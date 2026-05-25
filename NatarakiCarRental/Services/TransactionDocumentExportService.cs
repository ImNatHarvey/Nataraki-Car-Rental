using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Services;

public enum TransactionDocumentFormat
{
    StandardA4,
    Thermal80,
    Thermal57
}

public sealed class TransactionDocumentExportService
{
    private readonly TransactionService _transactionService;

    public TransactionDocumentExportService()
        : this(new TransactionService())
    {
    }

    public TransactionDocumentExportService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task ExportReceiptAsync(int transactionId, string path, TransactionDocumentFormat format, string generatedBy)
    {
        TransactionDocumentData data = await LoadDataAsync(transactionId, generatedBy);
        WritePdf(path, BuildPages(data, format, TransactionDocumentKind.Receipt), format);
    }

    public async Task ExportInvoiceAsync(int transactionId, string path, TransactionDocumentFormat format, string generatedBy)
    {
        TransactionDocumentData data = await LoadDataAsync(transactionId, generatedBy);
        WritePdf(path, BuildPages(data, format, TransactionDocumentKind.Invoice), format);
    }

    private async Task<TransactionDocumentData> LoadDataAsync(int transactionId, string generatedBy)
    {
        AccessControlService.EnforcePermission("Transactions.View");

        Transaction? transaction = await _transactionService.GetByIdAsync(transactionId);
        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction record was not found.");
        }

        IReadOnlyList<TransactionPaymentListItem> payments = await _transactionService.GetPaymentsAsync(transactionId);
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;
        return new TransactionDocumentData(transaction, payments, settings, CreateBusinessAddress(settings), generatedBy);
    }

    private static IReadOnlyList<string> BuildPages(TransactionDocumentData data, TransactionDocumentFormat format, TransactionDocumentKind kind)
    {
        return format == TransactionDocumentFormat.StandardA4
            ? BuildA4Pages(data, format, kind)
            : [BuildThermalPage(data, format, kind)];
    }

    private static IReadOnlyList<string> BuildA4Pages(TransactionDocumentData data, TransactionDocumentFormat format, TransactionDocumentKind kind)
    {
        List<string> pages = [];
        LogoData? logo = LoadLogoJpeg();
        A4Page page = new(logo);

        AddA4Header(page, data, logo is not null);
        page.CenterText(kind == TransactionDocumentKind.Receipt ? "RECEIPT" : "INVOICE", 16, bold: true);
        page.Move(24);

        Transaction transaction = data.Transaction;
        page.Section("Transaction Information");
        page.KeyValue("Transaction No", transaction.TransactionCode);
        page.KeyValue("Customer", ValueOrDash(transaction.CustomerName));
        page.KeyValue("Vehicle", ValueOrDash(transaction.CarName));
        page.KeyValue("Plate No", ValueOrDash(transaction.PlateNumber));
        page.KeyValue("Rental Period", $"{transaction.StartDate:yyyy-MM-dd} to {transaction.EndDate:yyyy-MM-dd}");
        page.KeyValue("Transaction Status", ValueOrDash(transaction.TransactionStatus));
        page.KeyValue("Payment Status", ValueOrDash(transaction.PaymentStatus));
        page.KeyValue("Mode of Payment", ValueOrDash(transaction.ModeOfPayment));
        page.KeyValue("Created Date", transaction.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        page.KeyValue("Processed By", ValueOrDash(data.GeneratedBy));
        page.Move(10);

        page.Section("Financial Summary");
        page.KeyValue("Rental Fee", FormatPeso(transaction.DailyRate * transaction.TotalDays));
        page.KeyValue("Additional Charges", FormatPeso(transaction.AdditionalCharge));
        page.KeyValue("Total Amount", FormatPeso(transaction.TotalAmount));
        page.KeyValue("Amount Paid", FormatPeso(transaction.AmountPaid));
        page.KeyValue("Balance", FormatPeso(transaction.BalanceAmount));
        page.Move(10);

        page.Section("Payment History");
        page.TableHeader(["Date", "Type", "Method", "Amount"], [108, 170, 130, 104]);
        if (data.Payments.Count == 0)
        {
            page.TableRow(["No payments recorded.", string.Empty, string.Empty, string.Empty], [108, 170, 130, 104]);
        }
        else
        {
            foreach (TransactionPaymentListItem payment in data.Payments.OrderBy(payment => payment.PaymentDate))
            {
                page.TableRow(
                    [
                        payment.PaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ValueOrDash(payment.PaymentCategory),
                        ValueOrDash(payment.ModeOfPayment),
                        FormatPeso(payment.Amount)
                    ],
                    [108, 170, 130, 104]);
            }
        }

        page.Move(24);
        page.CenterText($"Thank you for choosing {data.Settings.BusinessName}.", 10, bold: true);
        page.AddFooter(data.Settings.BusinessName);
        pages.Add(page.Content);
        return pages;
    }

    private static void AddA4Header(A4Page page, TransactionDocumentData data, bool hasLogo)
    {
        // Draw a solid theme-colored bar at the top (matching ReportExportService Letter size)
        page.Rect(0, 712, 612, 80, page.ThemeRgb);
        
        // Pure text header for A4 reports as requested - Strip the logo
        double y = 758;
        page.Text(data.Settings.BusinessName, 306, y, 18, bold: true, centered: true, color: "1 1 1");
        y -= 18;
        page.Text(data.ContactLine, 306, y, 9, centered: true, color: "0.95 0.95 0.95");
        
        page.Y = 680;
    }

    private static string BuildThermalPage(TransactionDocumentData data, TransactionDocumentFormat format, TransactionDocumentKind kind)
    {
        ThermalLayout layout = GetThermalLayout(format, data, kind);
        PdfPage page = new(layout.Width, layout.Height);
        double y = layout.Height - layout.Margin;

        page.Center(data.Settings.BusinessName, y, layout.HeaderFont, bold: true);
        y -= layout.LineHeight;
        foreach (string line in WrapText(data.ContactLine, layout.MaxChars))
        {
            page.Center(line, y, layout.SmallFont);
            y -= layout.LineHeight;
        }

        y -= 4;
        page.Center(kind == TransactionDocumentKind.Receipt ? "RECEIPT" : "INVOICE", y, layout.HeaderFont, bold: true);
        y -= layout.LineHeight;
        
        // Dash separator
        page.Center(new string('-', layout.MaxChars), y, layout.BodyFont, courier: true);
        y -= layout.LineHeight;

        Transaction transaction = data.Transaction;
        TransactionPaymentListItem? latestPayment = data.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
        DateTime paymentDate = latestPayment?.PaymentDate ?? transaction.CreatedAt;

        // Conditional Layout: Stacked for 57mm, Justified for 80mm
        bool isStacked = format == TransactionDocumentFormat.Thermal57;

        y = ThermalItem(page, layout, "Customer", ValueOrDash(transaction.CustomerName), y, isStacked);
        y = ThermalItem(page, layout, "Date", paymentDate.ToString("MMM d, yyyy"), y, isStacked);
        y = ThermalItem(page, layout, "Transaction ID", $"#{transaction.TransactionCode}", y, isStacked);
        y = ThermalItem(page, layout, "Vehicle", ValueOrDash(transaction.CarName), y, isStacked);
        y = ThermalItem(page, layout, "Plate No", ValueOrDash(transaction.PlateNumber), y, isStacked);
        
        // Dash separator
        page.Center(new string('-', layout.MaxChars), y, layout.BodyFont, courier: true);
        y -= layout.LineHeight;

        // Financial Values
        y = ThermalItem(page, layout, "TOTAL AMOUNT", FormatPeso(transaction.TotalAmount), y, isStacked, bold: true);
        y = ThermalItem(page, layout, kind == TransactionDocumentKind.Receipt ? "AMOUNT PAID" : "PAID", FormatPeso(transaction.AmountPaid), y, isStacked, bold: true);
        y = ThermalItem(page, layout, "BALANCE", FormatPeso(transaction.BalanceAmount), y, isStacked, bold: true);

        // Payment History Section
        if (data.Payments.Count > 0)
        {
            y -= 4;
            page.Center(new string('-', layout.MaxChars), y, layout.BodyFont, courier: true);
            y -= layout.LineHeight;
            page.Center("PAYMENT HISTORY", y, layout.BodyFont, bold: true);
            y -= layout.LineHeight;

            foreach (var payment in data.Payments.OrderByDescending(p => p.PaymentDate))
            {
                string dateMode = $"{payment.PaymentDate:MMM dd} - {payment.ModeOfPayment}";
                y = ThermalItem(page, layout, dateMode, FormatPeso(payment.Amount), y, isStacked);
            }
        }
        
        page.Center(new string('-', layout.MaxChars), y, layout.BodyFont, courier: true);
        y -= layout.LineHeight + 8;
        page.Center("Thank you for choosing us!", y, layout.BodyFont, bold: true);
        return page.Content;
    }

    private static double ThermalItem(PdfPage page, ThermalLayout layout, string label, string value, double y, bool stacked, bool bold = false)
    {
        string safeValue = SanitizePdfText(value);
        if (stacked)
        {
            // Stacked layout: Label on top, Value below
            page.Text(label, layout.Margin, y, layout.BodyFont, bold: bold, courier: true);
            y -= layout.LineHeight;
            page.Text(safeValue, layout.Margin + 4, y, layout.BodyFont, bold: bold, courier: true);
            return y - layout.LineHeight;
        }
        else
        {
            // Justified layout: Label left, Value right
            page.Text(label, layout.Margin, y, layout.BodyFont, bold: bold, courier: true);
            page.Text(safeValue, layout.Width - layout.Margin, y, layout.BodyFont, bold: bold, rightAligned: true, courier: true);
            return y - layout.LineHeight;
        }
    }

    private static ThermalLayout GetThermalLayout(TransactionDocumentFormat format, TransactionDocumentData data, TransactionDocumentKind kind)
    {
        double width = format == TransactionDocumentFormat.Thermal57 ? 162 : 226;
        double margin = format == TransactionDocumentFormat.Thermal57 ? 8 : 12;
        int maxChars = format == TransactionDocumentFormat.Thermal57 ? 24 : 32;
        
        const double lineHeight = 14;
        double y = margin;
        
        // Header
        y += lineHeight; // Business Name
        y += WrapText(data.ContactLine, maxChars).Count() * lineHeight;
        y += 4 + lineHeight; // Document Title
        y += lineHeight; // Separator

        // Details
        int detailLines = format == TransactionDocumentFormat.Thermal57 ? 10 : 5;
        y += detailLines * lineHeight;
        y += lineHeight; // Separator

        // Totals
        int totalLines = format == TransactionDocumentFormat.Thermal57 ? 6 : 3;
        y += totalLines * lineHeight;

        // Payment History
        if (data.Payments.Count > 0)
        {
            y += 4 + lineHeight; // Separator
            y += lineHeight; // PAYMENT HISTORY Header
            int paymentLines = format == TransactionDocumentFormat.Thermal57 ? data.Payments.Count * 2 : data.Payments.Count;
            y += paymentLines * lineHeight;
        }

        y += lineHeight; // Separator
        y += lineHeight + 8; // Footer
        y += margin;

        return new ThermalLayout(width, y, margin, maxChars, 8, 9, 10, lineHeight, 0);
    }

    private static void WritePdf(string path, IReadOnlyList<string> pageContents, TransactionDocumentFormat format)
    {
        LogoData? logo = format == TransactionDocumentFormat.StandardA4 ? LoadLogoJpeg() : null;
        PdfSize pageSize = format == TransactionDocumentFormat.StandardA4
            ? new PdfSize(612, 792) // Letter size
            : GetThermalPageSize(pageContents[0]);

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        List<long> offsets = [];
        int pageCount = pageContents.Count;
        int regularFontObject = 3 + (pageCount * 2);
        int boldFontObject = regularFontObject + 1;
        int courierFontObject = boldFontObject + 1;
        int imageObject = logo is null ? 0 : courierFontObject + 1;

        writer.WriteLine("%PDF-1.4");
        WritePdfObject(writer, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WritePdfObject(writer, offsets, 2, $"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"))}] /Count {pageCount} >>");
        for (int index = 0; index < pageCount; index++)
        {
            int pageObject = 3 + (index * 2);
            int contentObject = pageObject + 1;
            string imageResource = (format == TransactionDocumentFormat.StandardA4 && logo is not null) ? $" /XObject << /Im1 {imageObject} 0 R >>" : string.Empty;
            WritePdfObject(writer, offsets, pageObject, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageSize.Width.ToString(CultureInfo.InvariantCulture)} {pageSize.Height.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 {regularFontObject} 0 R /F2 {boldFontObject} 0 R /F3 {courierFontObject} 0 R >>{imageResource} >> /Contents {contentObject} 0 R >>");
            WritePdfStreamObject(writer, offsets, contentObject, pageContents[index]);
        }

        WritePdfObject(writer, offsets, regularFontObject, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        WritePdfObject(writer, offsets, boldFontObject, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
        WritePdfObject(writer, offsets, courierFontObject, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        if (format == TransactionDocumentFormat.StandardA4 && logo is not null)
        {
            WritePdfBinaryStreamObject(writer, offsets, imageObject, $"<< /Type /XObject /Subtype /Image /Width {logo.Width} /Height {logo.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {logo.Bytes.Length} >>", logo.Bytes);
        }

        writer.Flush();
        long xrefOffset = writer.BaseStream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {offsets.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (long offset in offsets)
        {
            writer.WriteLine($"{offset:0000000000} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefOffset);
        writer.WriteLine("%%EOF");
    }

    private static PdfSize GetThermalPageSize(string pageContent)
    {
        string marker = "%SIZE ";
        string firstLine = pageContent.Split('\n').FirstOrDefault() ?? string.Empty;
        if (firstLine.StartsWith(marker, StringComparison.Ordinal))
        {
            string[] parts = firstLine[marker.Length..].Trim().Split('x');
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double height))
            {
                return new PdfSize(width, height);
            }
        }

        return new PdfSize(226, 520);
    }

    private static void WritePdfObject(StreamWriter writer, List<long> offsets, int objectNumber, string value)
    {
        writer.Flush();
        offsets.Add(writer.BaseStream.Position);
        writer.WriteLine($"{objectNumber} 0 obj");
        writer.WriteLine(value);
        writer.WriteLine("endobj");
    }

    private static void WritePdfStreamObject(StreamWriter writer, List<long> offsets, int objectNumber, string content)
    {
        writer.Flush();
        offsets.Add(writer.BaseStream.Position);
        byte[] bytes = Encoding.ASCII.GetBytes(content);
        writer.WriteLine($"{objectNumber} 0 obj");
        writer.WriteLine($"<< /Length {bytes.Length} >>");
        writer.WriteLine("stream");
        writer.Flush();
        writer.BaseStream.Write(bytes, 0, bytes.Length);
        writer.WriteLine();
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");
    }

    private static void WritePdfBinaryStreamObject(StreamWriter writer, List<long> offsets, int objectNumber, string dictionary, byte[] bytes)
    {
        writer.Flush();
        offsets.Add(writer.BaseStream.Position);
        writer.WriteLine($"{objectNumber} 0 obj");
        writer.WriteLine(dictionary);
        writer.WriteLine("stream");
        writer.Flush();
        writer.BaseStream.Write(bytes, 0, bytes.Length);
        writer.WriteLine();
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");
    }

    private static LogoData? LoadLogoJpeg()
    {
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;
        string? logoPath = settings.SystemIconPath;

        // Skip logo if explicitly set to None
        if (string.Equals(settings.SystemLogoMode, "None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        Image? logoImage = null;
        try
        {
            // Only attempt to load if the file actually exists
            if (!string.IsNullOrWhiteSpace(logoPath) && System.IO.File.Exists(logoPath))
            {
                logoImage = Image.FromFile(logoPath);
            }
            else
            {
                return null;
            }
        }
        catch
        {
            logoImage?.Dispose();
            return null; // Gracefully skip logo on any error
        }

        if (logoImage == null) return null;

        using (logoImage)
        {
            const int maxSize = 192;
            int width;
            int height;
            if (logoImage.Width >= logoImage.Height)
            {
                width = maxSize;
                height = Math.Max(1, (int)Math.Round(logoImage.Height * (maxSize / (double)logoImage.Width)));
            }
            else
            {
                height = maxSize;
                width = Math.Max(1, (int)Math.Round(logoImage.Width * (maxSize / (double)logoImage.Height)));
            }

            using Bitmap bitmap = new(width, height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.DrawImage(logoImage, new Rectangle(0, 0, width, height));
            }

            using MemoryStream memory = new();
            bitmap.Save(memory, ImageFormat.Jpeg);
            return new LogoData(memory.ToArray(), width, height);
        }
    }

    private static string CreateBusinessAddress(SystemSettingsModel settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BusinessAddress))
        {
            return settings.BusinessAddress.Trim();
        }

        string[] parts =
        [
            settings.BusinessStreetAddress,
            settings.BusinessBarangayName,
            settings.BusinessCityName,
            settings.BusinessProvinceName,
            settings.BusinessRegionName
        ];
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }

    private static string CreateBusinessContactLine(TransactionDocumentData data)
    {
        string[] parts = [data.Settings.ContactNumber, data.Settings.EmailAddress, data.BusinessAddress];
        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }

    private static IEnumerable<string> WrapText(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield return "-";
            yield break;
        }

        string remaining = value.Trim();
        while (remaining.Length > maxChars)
        {
            int split = remaining.LastIndexOf(' ', Math.Min(maxChars, remaining.Length - 1));
            if (split <= 0)
            {
                split = maxChars;
            }

            yield return remaining[..split].TrimEnd();
            remaining = remaining[split..].TrimStart();
        }

        yield return remaining;
    }

    private static string Separator(int length) => new('-', length);

    private static string FormatPeso(decimal amount) => $"PHP {amount:N2}";

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string EscapePdfText(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string SanitizePdfText(string value)
    {
        string sanitized = value
            .Replace("₱", "PHP ", StringComparison.Ordinal)
            .Replace("â‚±", "PHP ", StringComparison.Ordinal)
            .Replace("Ã¢â€šÂ±", "PHP ", StringComparison.Ordinal);
        return new string(sanitized.Select(character => character <= 127 ? character : '?').ToArray());
    }

    private enum TransactionDocumentKind
    {
        Receipt,
        Invoice
    }

    private sealed record TransactionDocumentData(
        Transaction Transaction,
        IReadOnlyList<TransactionPaymentListItem> Payments,
        SystemSettingsModel Settings,
        string BusinessAddress,
        string GeneratedBy)
    {
        public string ContactLine => CreateBusinessContactLine(this);
    }

    private sealed record LogoData(byte[] Bytes, int Width, int Height);

    private sealed record PdfSize(double Width, double Height);

    private sealed record ThermalLayout(
        double Width,
        double Height,
        double Margin,
        int MaxChars,
        int SmallFont,
        int BodyFont,
        int HeaderFont,
        double LineHeight,
        double LogoSize);

    private class PdfPage
    {
        private readonly StringBuilder _builder = new();
        private readonly double _width;

        public PdfPage(double width, double height)
        {
            _width = width;
            _builder.AppendLine(FormattableString.Invariant($"%SIZE {width:0.##}x{height:0.##}"));
        }

        public string Content => _builder.ToString();

        public void Text(string text, double x, double y, int size, bool bold = false, bool centered = false, bool rightAligned = false, string color = "0 0 0", bool courier = false)
        {
            string safe = EscapePdfText(SanitizePdfText(text));
            double width = EstimateTextWidth(text, size, courier);
            if (centered)
            {
                x -= width / 2;
            }
            else if (rightAligned)
            {
                x -= width;
            }

            _builder.AppendLine($"{color} rg");
            _builder.AppendLine("BT");
            string font = courier ? "F3" : bold ? "F2" : "F1";
            _builder.AppendLine($"/{font} {size} Tf");
            _builder.AppendLine(FormattableString.Invariant($"{x:0.##} {y:0.##} Td"));
            _builder.AppendLine($"({safe}) Tj");
            _builder.AppendLine("ET");
        }

        public void Center(string text, double y, int size, bool bold = false, bool courier = false)
        {
            Text(text, _width / 2, y, size, bold, centered: true, courier: courier, color: "0 0 0");
        }

        public void Rect(double x, double y, double width, double height, string color)
        {
            _builder.AppendLine($"{color} rg");
            _builder.AppendLine(FormattableString.Invariant($"{x:0.##} {y:0.##} {width:0.##} {height:0.##} re f"));
        }

        public void Line(double x1, double y1, double x2, double y2, string color, double width = 0.5)
        {
            _builder.AppendLine($"{color} RG");
            _builder.AppendLine(FormattableString.Invariant($"{width:0.##} w"));
            _builder.AppendLine(FormattableString.Invariant($"{x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S"));
        }

        public void Image(string name, double x, double y, double width, double height)
        {
            _builder.AppendLine("q");
            _builder.AppendLine(FormattableString.Invariant($"{width:0.##} 0 0 {height:0.##} {x:0.##} {y:0.##} cm"));
            _builder.AppendLine($"/{name} Do");
            _builder.AppendLine("Q");
        }

        protected static double EstimateTextWidth(string text, int size, bool courier = false) => text.Length * size * (courier ? 0.60 : 0.48);
    }

    private sealed class A4Page : PdfPage
    {
        private const double Left = 50;
        private const double Right = 562;
        private readonly string _themeRgb;

        public A4Page(LogoData? logo) : base(612, 792) // Letter size
        {
            Color primary = ThemeHelper.Primary;
            _themeRgb = $"{primary.R / 255d:0.###} {primary.G / 255d:0.###} {primary.B / 255d:0.###}";
            Y = 680;
        }

        public double Y { get; set; }
        public string ThemeRgb => _themeRgb;

        public void AccentBox(double x, double y, double width, double height) => Rect(x, y, width, height, _themeRgb);

        public void Move(double amount) => Y -= amount;

        public void CenterText(string text, int size, bool bold = false)
        {
            Text(text, 306, Y, size, bold, centered: true);
            Y -= size + 10;
        }

        public void Section(string title)
        {
            Rect(Left, Y - 4, Right - Left, 18, _themeRgb);
            Text(title, Left + 8, Y + 1, 10, bold: true, color: "1 1 1");
            Y -= 28;
        }

        public void KeyValue(string label, string value)
        {
            Text($"{label}:", Left + 8, Y, 9, bold: true);
            foreach (string line in WrapText(value, 64))
            {
                Text(line, Left + 150, Y, 9);
                Y -= 14;
            }
            Line(Left, Y + 5, Right, Y + 5, "0.90 0.93 0.96");
        }

        public void TableHeader(IReadOnlyList<string> columns, IReadOnlyList<double> widths)
        {
            Rect(Left, Y - 5, Right - Left, 18, _themeRgb);
            double x = Left + 6;
            for (int i = 0; i < columns.Count; i++)
            {
                Text(columns[i], x, Y, 8, bold: true, color: "1 1 1");
                x += widths[i];
            }
            Y -= 24;
        }

        public void TableRow(IReadOnlyList<string> values, IReadOnlyList<double> widths)
        {
            double x = Left + 6;
            double startY = Y;
            double maxY = Y - 14;
            for (int i = 0; i < values.Count; i++)
            {
                int maxChars = i switch
                {
                    1 => 28,
                    2 => 20,
                    _ => 16
                };
                double cellY = startY;
                foreach (string line in WrapText(values[i], maxChars))
                {
                    Text(line, x, cellY, 8);
                    cellY -= 12;
                }
                maxY = Math.Min(maxY, cellY);
                x += widths[i];
            }
            Y = maxY - 2;
            Line(Left, Y + 7, Right, Y + 7, "0.90 0.93 0.96");
        }

        public void AddFooter(string businessName)
        {
            Line(Left, 42, Right, 42, "0.80 0.84 0.90");
            Text($"Generated by {businessName}", Left, 28, 8, color: "0.39 0.45 0.55");
            Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Right, 28, 8, rightAligned: true, color: "0.39 0.45 0.55");
        }
    }
}
