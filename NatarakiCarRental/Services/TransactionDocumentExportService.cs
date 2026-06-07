using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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

    static TransactionDocumentExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public TransactionDocumentExportService() : this(new TransactionService()) { }

    public TransactionDocumentExportService(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    // Theme Color Helpers
    private static QuestPDF.Infrastructure.Color ThemePrimary => QuestPDF.Infrastructure.Color.FromRGB(ThemeHelper.Primary.R, ThemeHelper.Primary.G, ThemeHelper.Primary.B);
    private static QuestPDF.Infrastructure.Color ThemePrimaryDark => QuestPDF.Infrastructure.Color.FromRGB(ThemeHelper.PrimaryHover.R, ThemeHelper.PrimaryHover.G, ThemeHelper.PrimaryHover.B);
    private static QuestPDF.Infrastructure.Color ThemeContrastText => GetContrastTextColor(ThemeHelper.Primary);

    private static QuestPDF.Infrastructure.Color ToQuestColor(System.Drawing.Color color) => QuestPDF.Infrastructure.Color.FromRGB(color.R, color.G, color.B);

    private static QuestPDF.Infrastructure.Color GetContrastTextColor(System.Drawing.Color bgColor)
    {
        double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
        return luminance > 0.6 ? Colors.Black : Colors.White;
    }

    public async Task ExportReceiptAsync(int transactionId, string path, TransactionDocumentFormat format, string generatedBy)
    {
        var data = await LoadDataAsync(transactionId, generatedBy);
        if (format == TransactionDocumentFormat.StandardA4)
        {
            await ExportToPdfAsync(path, data);
        }
        else
        {
            await ExportToThermalPdfAsync(path, format, data);
        }
    }

    public async Task ExportInvoiceAsync(int transactionId, string path, TransactionDocumentFormat format, string generatedBy)
    {
        var data = await LoadDataAsync(transactionId, generatedBy);
        if (format == TransactionDocumentFormat.StandardA4)
        {
            await ExportToPdfAsync(path, data);
        }
        else
        {
            await ExportToThermalPdfAsync(path, format, data);
        }
    }

    private async Task<TransactionDocumentData> LoadDataAsync(int transactionId, string generatedBy)
    {
        AccessControlService.EnforcePermission("Transactions.View");

        Transaction? transaction = await _transactionService.GetByIdAsync(transactionId);
        if (transaction is null) throw new InvalidOperationException("Transaction record was not found.");

        IReadOnlyList<TransactionPaymentListItem> payments = await _transactionService.GetPaymentsAsync(transactionId);
        SystemSettingsModel settings = AppBrandingManager.CurrentSettings;

        // Dynamic Title Logic
        bool isFullyPaid = transaction.BalanceAmount <= 0 || 
                           transaction.TransactionStatus == TransactionConstants.Status.Completed ||
                           transaction.PaymentStatus == TransactionConstants.PaymentStatus.Paid;
        
        string documentTitle = isFullyPaid ? "RECEIPT" : "INVOICE";

        return new TransactionDocumentData(
            transaction, 
            payments.OrderBy(p => p.PaymentDate).ToList(), 
            settings, 
            CreateBusinessAddress(settings), 
            generatedBy,
            documentTitle,
            isFullyPaid);
    }

    // A4 PDF Generation (Portrait)
    private async Task ExportToPdfAsync(string path, TransactionDocumentData data)
    {
        await Task.Run(() =>
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                        ComposeHeader(page.Header(), data);
                        ComposeContent(page.Content(), data);
                        ComposeFooter(page.Footer(), data.Settings.BusinessName);
                    });
                });

                document.GeneratePdf(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"QuestPDF failed to generate {data.DocumentTitle.ToLower()}: {ex.Message}", ex);
            }
        });
    }

    // Thermal PDF Generation (Dynamic Height)
    private async Task ExportToThermalPdfAsync(string path, TransactionDocumentFormat format, TransactionDocumentData data)
    {
        float width = format == TransactionDocumentFormat.Thermal80 ? 226 : 164; // Approx 80mm and 57mm in points
        
        await Task.Run(() =>
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.ContinuousSize(width);
                        page.Margin(0.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(format == TransactionDocumentFormat.Thermal80 ? 9 : 8).FontFamily(Fonts.Verdana));

                        page.Content().Column(column =>
                        {
                            ComposeThermalHeader(column, data);
                            ComposeThermalCustomerSection(column, data);
                            ComposeThermalItemsTable(column, data);
                            ComposeThermalPaymentHistory(column, data, format == TransactionDocumentFormat.Thermal57);
                            ComposeThermalTotals(column, data);
                            ComposeThermalFooter(column, data);
                        });
                    });
                });

                document.GeneratePdf(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"QuestPDF failed to generate thermal {data.DocumentTitle.ToLower()}: {ex.Message}", ex);
            }
        });
    }

    // A4 Components
    private void ComposeHeader(IContainer container, TransactionDocumentData data)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(data.Settings.BusinessName).FontSize(16).SemiBold().FontColor(ThemePrimary);
                col.Item().Text(data.BusinessAddress).FontSize(8).FontColor(Colors.Grey.Medium);
                col.Item().Text($"Contact: {data.Settings.ContactNumber}").FontSize(8).FontColor(Colors.Grey.Medium);
                if (!string.IsNullOrWhiteSpace(data.Settings.EmailAddress))
                    col.Item().Text(data.Settings.EmailAddress).FontSize(8).FontColor(Colors.Grey.Medium);
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text(data.DocumentTitle).FontSize(24).ExtraBold().FontColor(ThemePrimaryDark);
                col.Item().Text(x =>
                {
                    x.Span("No: ").SemiBold();
                    x.Span(data.Transaction.TransactionCode).SemiBold().FontColor(ThemePrimaryDark);
                });
                col.Item().Text($"Generated: {DateTime.Now:MMM d, yyyy h:mm tt}").FontSize(8);
                col.Item().Text($"Transaction Date: {data.Transaction.CreatedAt:MMM d, yyyy}").FontSize(8);
            });
        });
    }

    private void ComposeContent(IContainer container, TransactionDocumentData data)
    {
        container.PaddingTop(15).Column(column =>
        {
            // Customer & Rental Information Cards
            column.Item().Row(row =>
            {
                row.RelativeItem().PaddingRight(10).Column(c =>
                {
                    c.Item().Text("CUSTOMER INFORMATION").FontSize(10).SemiBold().FontColor(ThemePrimaryDark);
                    c.Item().PaddingTop(4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(inner =>
                    {
                        inner.Item().Text(data.Transaction.CustomerName).SemiBold();
                        inner.Item().Text(data.Transaction.CustomerPhone ?? "-");
                        inner.Item().Text(data.Transaction.CustomerAddress ?? "-").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });

                row.RelativeItem().PaddingLeft(10).Column(c =>
                {
                    c.Item().Text("RENTAL DETAILS").FontSize(10).SemiBold().FontColor(ThemePrimaryDark);
                    c.Item().PaddingTop(4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(inner =>
                    {
                        inner.Item().Text(FormattingHelper.CarPlate(data.Transaction.CarName, data.Transaction.PlateNumber)).SemiBold();
                        inner.Item().Text(x => { x.Span("Period: ").FontSize(8).FontColor(Colors.Grey.Medium); x.Span($"{data.Transaction.StartDate:MMM d} - {data.Transaction.EndDate:MMM d, yyyy}").FontSize(8); });
                        inner.Item().Text(x => { x.Span("Duration: ").FontSize(8).FontColor(Colors.Grey.Medium); x.Span($"{data.Transaction.TotalDays} day(s)").FontSize(8); });
                        inner.Item().Text(x => { x.Span("Status: ").FontSize(8).FontColor(Colors.Grey.Medium); x.Span(data.Transaction.TransactionStatus).FontSize(8).SemiBold().FontColor(ThemePrimary); });
                    });
                });
            });

            // Payment Breakdown / Items Table
            column.Item().PaddingTop(20).Text("ITEMIZED CHARGES").FontSize(10).SemiBold().FontColor(ThemePrimaryDark);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background(ThemePrimaryDark).Padding(4).Text("Description").FontColor(ThemeContrastText).SemiBold();
                    header.Cell().Background(ThemePrimaryDark).Padding(4).Text("Qty/Days").FontColor(ThemeContrastText).SemiBold().AlignCenter();
                    header.Cell().Background(ThemePrimaryDark).Padding(4).Text("Rate").FontColor(ThemeContrastText).SemiBold().AlignRight();
                    header.Cell().Background(ThemePrimaryDark).Padding(4).Text("Amount").FontColor(ThemeContrastText).SemiBold().AlignRight();
                });

                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text($"Base Rental Fee ({data.Transaction.CarName})");
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(data.Transaction.TotalDays.ToString());
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.DailyRate));
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.DailyRate * data.Transaction.TotalDays));

                if (data.Transaction.AdditionalCharge > 0)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text("Additional Charges / Penalties");
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text("-");
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("-");
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.AdditionalCharge));
                }
            });

            // Payment History
            if (data.Payments.Any())
            {
                column.Item().PaddingTop(20).Text("PAYMENT HISTORY").FontSize(10).SemiBold().FontColor(ThemePrimaryDark);
                column.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().BorderBottom(1).Padding(4).Text("Date & Time").SemiBold();
                        header.Cell().BorderBottom(1).Padding(4).Text("Method").SemiBold();
                        header.Cell().BorderBottom(1).Padding(4).Text("Reference No.").SemiBold();
                        header.Cell().BorderBottom(1).Padding(4).Text("Amount").SemiBold().AlignRight();
                    });

                    foreach (var p in data.Payments)
                    {
                        table.Cell().Padding(4).Text(p.PaymentDate.ToString("yyyy-MM-dd hh:mm tt")).FontSize(8);
                        table.Cell().Padding(4).Text(p.ModeOfPayment).FontSize(8);
                        table.Cell().Padding(4).Text(p.ReferenceNumber ?? "N/A").FontSize(8);
                        table.Cell().Padding(4).AlignRight().Text(FormattingHelper.FormatPeso(p.Amount)).FontSize(8).SemiBold();
                    }
                });
            }

            // Summary Panel
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem(2);
                row.RelativeItem().Padding(4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).BorderTop(2).BorderColor(ThemePrimary).Background(Colors.Grey.Lighten4).Column(c =>
                {
                    c.Item().Row(r => { r.RelativeItem().Text("SUBTOTAL").FontSize(7).SemiBold().FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.TotalAmount)).FontSize(8).SemiBold(); });
                    c.Item().Row(r => { r.RelativeItem().Text("TOTAL PAID").FontSize(7).SemiBold().FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.AmountPaid)).FontSize(8).SemiBold().FontColor(ThemePrimary); });
                    c.Item().PaddingTop(4).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).Row(r => 
                    { 
                        r.RelativeItem().Text("REMAINING BALANCE").FontSize(8).Bold().FontColor(ThemePrimaryDark); 
                        r.RelativeItem().AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.BalanceAmount)).FontSize(10).ExtraBold().FontColor(ThemePrimaryDark); 
                    });
                    
                    c.Item().PaddingTop(4).AlignCenter().Text(data.IsFullyPaid ? "STATUS: PAID" : "STATUS: PARTIAL / UNPAID")
                        .FontSize(9).Bold().FontColor(data.IsFullyPaid ? ToQuestColor(ThemeHelper.Success) : ToQuestColor(ThemeHelper.Danger));
                });
            });

            column.Item().PaddingTop(25).Column(c =>
            {
                c.Item().Text("OFFICIAL REMARKS").FontSize(10).SemiBold().FontColor(ThemePrimaryDark);
                c.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(data.Transaction.Notes) ? "No additional notes provided." : data.Transaction.Notes).FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                c.Item().PaddingTop(10).Text(x => { x.Span("Processed By: ").SemiBold(); x.Span(data.GeneratedBy); });
            });
        });
    }

    private void ComposeFooter(IContainer container, string businessName)
    {
        container.PaddingTop(10).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.RelativeItem().Text(x => { x.Span($"Thank you for choosing {businessName}!").SemiBold().FontColor(ThemePrimary); });
            row.RelativeItem().AlignRight().Text(x => { x.Span("Generated by NatarakiCarRental System | Page ").FontSize(8).FontColor(Colors.Grey.Medium); x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium); });
        });
    }

    // Thermal Components
    private void ComposeThermalHeader(ColumnDescriptor column, TransactionDocumentData data)
    {
        column.Item().AlignCenter().Column(c =>
        {
            c.Item().AlignCenter().Text(data.Settings.BusinessName).FontSize(12).ExtraBold().FontColor(ThemePrimary);
            c.Item().AlignCenter().Text(data.BusinessAddress).FontSize(7);
            c.Item().AlignCenter().Text($"Tel: {data.Settings.ContactNumber}").FontSize(7);
            c.Item().PaddingVertical(5).AlignCenter().Text(data.DocumentTitle).FontSize(14).ExtraBold().FontColor(ThemePrimaryDark);
            
            c.Item().AlignCenter().Text(text =>
            {
                text.Span("No: ").SemiBold();
                text.Span(data.Transaction.TransactionCode).SemiBold();
                text.DefaultTextStyle(x => x.FontSize(8));
            });
            
            c.Item().AlignCenter().Text(DateTime.Now.ToString("MMM d, yyyy h:mm tt")).FontSize(7);
        });
        column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    private void ComposeThermalCustomerSection(ColumnDescriptor column, TransactionDocumentData data)
    {
        column.Item().Column(c =>
        {
            c.Item().Text(x => { x.Span("Customer: ").SemiBold(); x.Span(data.Transaction.CustomerName); });
            c.Item().Text(x => { x.Span("Vehicle: ").SemiBold(); x.Span(FormattingHelper.CarPlate(data.Transaction.CarName, data.Transaction.PlateNumber)); });
            c.Item().Text(x => { x.Span("Duration: ").SemiBold(); x.Span($"{data.Transaction.TotalDays} day(s) ({data.Transaction.StartDate:MMM d} - {data.Transaction.EndDate:MMM d})"); });
        });
        column.Item().PaddingVertical(5).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
    }

    private void ComposeThermalItemsTable(ColumnDescriptor column, TransactionDocumentData data)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Text("Rental Fee").SemiBold();
            table.Cell().AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.DailyRate * data.Transaction.TotalDays));

            if (data.Transaction.AdditionalCharge > 0)
            {
                table.Cell().Text("Penalties/Addl").SemiBold();
                table.Cell().AlignRight().Text(FormattingHelper.FormatPeso(data.Transaction.AdditionalCharge));
            }
        });
        column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(ThemePrimary);
    }

    private void ComposeThermalPaymentHistory(ColumnDescriptor column, TransactionDocumentData data, bool isCompact)
    {
        if (!data.Payments.Any()) return;

        column.Item().PaddingTop(5).Text("PAYMENT HISTORY").FontSize(8).SemiBold().FontColor(ThemePrimaryDark);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1.5f);
            });

            foreach (var p in data.Payments)
            {
                string methodLabel = isCompact ? (p.ModeOfPayment.Length > 5 ? p.ModeOfPayment[..5] : p.ModeOfPayment) : p.ModeOfPayment;
                string refLabel = isCompact ? "Ref:" : "Ref No:";
                
                table.Cell().Text(p.PaymentDate.ToString("MM/dd HH:mm")).FontSize(7);
                table.Cell().Text($"{methodLabel} {(string.IsNullOrWhiteSpace(p.ReferenceNumber) ? "" : $"({p.ReferenceNumber})")}").FontSize(7);
                table.Cell().AlignRight().Text(FormattingHelper.FormatPeso(p.Amount)).FontSize(7).SemiBold();
            }
        });
        column.Item().PaddingVertical(5).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
    }

    private void ComposeThermalTotals(ColumnDescriptor column, TransactionDocumentData data)
    {
        column.Item().AlignRight().Column(c =>
        {
            c.Item().Text(x => { x.Span("TOTAL: ").FontSize(11).ExtraBold().FontColor(ThemePrimaryDark); x.Span(FormattingHelper.FormatPeso(data.Transaction.TotalAmount)).FontSize(11).ExtraBold().FontColor(ThemePrimaryDark); });
            c.Item().Text(x => { x.Span("PAID: ").SemiBold(); x.Span(FormattingHelper.FormatPeso(data.Transaction.AmountPaid)).SemiBold().FontColor(ThemePrimary); });
            c.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            c.Item().Text(x => { x.Span("BALANCE: ").FontSize(10).ExtraBold(); x.Span(FormattingHelper.FormatPeso(data.Transaction.BalanceAmount)).FontSize(10).ExtraBold(); });
            
            c.Item().PaddingTop(5).AlignCenter().Text(data.IsFullyPaid ? "STATUS: PAID" : "STATUS: UNSETTLED")
                .FontSize(10).Bold().FontColor(data.IsFullyPaid ? ToQuestColor(ThemeHelper.Success) : ToQuestColor(ThemeHelper.Danger));
        });
    }

    private void ComposeThermalFooter(ColumnDescriptor column, TransactionDocumentData data)
    {
        column.Item().PaddingTop(15).AlignCenter().Column(c =>
        {
            c.Item().AlignCenter().Text("Thank you for choosing us!").SemiBold();
            c.Item().AlignCenter().Text("Drive safely and see you again.").FontSize(7);
            c.Item().PaddingTop(5).AlignCenter().Text("Generated by NatarakiCarRental").FontSize(6).FontColor(Colors.Grey.Medium);
        });
    }

    private static string CreateBusinessAddress(SystemSettingsModel settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BusinessAddress)) return settings.BusinessAddress.Trim();
        string[] parts = [settings.BusinessStreetAddress, settings.BusinessBarangayName, settings.BusinessCityName, settings.BusinessProvinceName, settings.BusinessRegionName];
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }

    private sealed record TransactionDocumentData(
        Transaction Transaction,
        IReadOnlyList<TransactionPaymentListItem> Payments,
        SystemSettingsModel Settings,
        string BusinessAddress,
        string GeneratedBy,
        string DocumentTitle,
        bool IsFullyPaid);
}
