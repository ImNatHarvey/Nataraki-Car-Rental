using System.Diagnostics;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Services;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsExportsTab : UserControl, IReportTab
{
    private readonly EnterpriseReportExportService _reportExportService = new();
    private readonly FlowLayoutPanel _layout = new();
    private DateTime _from;
    private DateTime _to;

    public ReportsExportsTab()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        ReportLayoutHelper.ConfigureReportPage(this);
        InitializeLayout();
        Resize += (_, _) => ResizeCards();
    }

    public Task LoadAsync(DateTime from, DateTime to)
    {
        _from = from;
        _to = to;
        ResizeCards();
        return Task.CompletedTask;
    }

    private void InitializeLayout()
    {
        _layout.Dock = DockStyle.Top;
        _layout.AutoSize = true;
        _layout.FlowDirection = FlowDirection.TopDown;
        _layout.WrapContents = false;
        _layout.Padding = new Padding(0, 0, 0, 28);
        _layout.BackColor = ThemeHelper.ContentBackground;

        _layout.Controls.Add(CreateExportSection(
            "Full Reports Bundle",
            "Combined printable summary PDF or one Excel workbook with the main report sheets (Financial, Fleet, Operations, Customers).",
            "Full_Bundle",
            _reportExportService.ExportFullPdfAsync,
            _reportExportService.ExportFullExcelAsync));

        _layout.Controls.Add(CreateExportSection(
            "Financial Reports",
            "Comprehensive financial summary, revenue breakdowns by category and payment method, and vehicle profitability analysis.",
            "Financial",
            _reportExportService.ExportFinancialPdfAsync,
            _reportExportService.ExportFinancialExcelAsync));

        _layout.Controls.Add(CreateExportSection(
            "Fleet Performance",
            "Vehicle utilization metrics, revenue per car, maintenance history, and availability analytics.",
            "Fleet",
            _reportExportService.ExportFleetPdfAsync,
            _reportExportService.ExportFleetExcelAsync));

        _layout.Controls.Add(CreateExportSection(
            "Operations Reports",
            "Daily operational data including upcoming/late returns, active rentals, and pending reservations.",
            "Operations",
            _reportExportService.ExportOperationsPdfAsync,
            _reportExportService.ExportOperationsExcelAsync));

        _layout.Controls.Add(CreateExportSection(
            "Customer Analytics",
            "Detailed customer rental history, outstanding balances, frequent renters, and blacklist reports.",
            "Customer",
            _reportExportService.ExportCustomerPdfAsync,
            _reportExportService.ExportCustomerExcelAsync));

        if (AccessControlService.HasPermission("ManageSystem.ActivityLogs"))
        {
            _layout.Controls.Add(CreateExportSection(
                "System Audit Log",
                "Compliance-ready activity log detailing user actions, security changes, and data modifications.",
                "Audit_Log",
                _reportExportService.ExportAuditPdfAsync,
                _reportExportService.ExportAuditExcelAsync));
        }

        Controls.Add(_layout);
        ResizeCards();
    }

    private Panel CreateExportSection(
        string title,
        string description,
        string fileNamePrefix,
        Func<string, DateTime, DateTime, string, Task> pdfExporter,
        Func<string, DateTime, DateTime, string, Task> excelExporter)
    {
        Panel card = ControlFactory.CreateCardPanel(new Size(900, 116));
        card.Height = 116;
        card.Padding = new Padding(20);
        card.Margin = new Padding(0, 0, 0, 18);

        Label titleLabel = new()
        {
            Text = title,
            AutoSize = false,
            Location = new Point(20, 18),
            Size = new Size(520, 24),
            Font = FontHelper.SemiBold(12F),
            ForeColor = ThemeHelper.TextPrimary
        };

        Label descriptionLabel = new()
        {
            Text = description,
            AutoSize = false,
            Location = new Point(20, 48),
            Size = new Size(560, 42),
            Font = FontHelper.Regular(9.5F),
            ForeColor = ThemeHelper.TextSecondary
        };

        Button pdfButton = ControlFactory.CreatePrimaryButton("Export PDF", 126, 36);
        Button excelButton = ControlFactory.CreatePrimaryButton("Export Excel", 136, 36);

        pdfButton.Click += async (_, _) => await ExportReportAsync(fileNamePrefix, "pdf", "PDF files (*.pdf)|*.pdf", pdfExporter, pdfButton);
        excelButton.Click += async (_, _) => await ExportReportAsync(fileNamePrefix, "xlsx", "Excel Workbook (*.xlsx)|*.xlsx", excelExporter, excelButton);

        card.Controls.Add(titleLabel);
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(pdfButton);
        card.Controls.Add(excelButton);
        card.Resize += (_, _) => LayoutExportCard(card, titleLabel, descriptionLabel, pdfButton, excelButton);
        LayoutExportCard(card, titleLabel, descriptionLabel, pdfButton, excelButton);
        return card;
    }

    private static void LayoutExportCard(Panel card, Label titleLabel, Label descriptionLabel, Button pdfButton, Button excelButton)
    {
        int right = card.ClientSize.Width - card.Padding.Right;
        excelButton.Location = new Point(right - excelButton.Width, 40);
        pdfButton.Location = new Point(excelButton.Left - pdfButton.Width - 12, 40);
        int textWidth = Math.Max(320, pdfButton.Left - descriptionLabel.Left - 24);
        titleLabel.Width = textWidth;
        descriptionLabel.Width = textWidth;
    }

    private void ResizeCards()
    {
        int width = Math.Max(600, ClientSize.Width - Padding.Left - Padding.Right - SystemInformation.VerticalScrollBarWidth);
        foreach (Control control in _layout.Controls)
        {
            control.Width = width;
        }
    }

    private async Task ExportReportAsync(
        string fileNamePrefix,
        string extension,
        string filter,
        Func<string, DateTime, DateTime, string, Task> exporter,
        Button sourceButton)
    {
        if (!AccessControlService.HasPermission("Reports.Export"))
        {
            MessageBoxHelper.ShowWarning("You do not have permission to export reports.", "Export Reports");
            return;
        }

        if (_to < _from)
        {
            MessageBoxHelper.ShowWarning("The report end date must be after the start date.", "Export Reports");
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = filter,
            FileName = BuildExportFileName(fileNamePrefix, extension),
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            sourceButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            string generatedBy = AccessControlService.CurrentUser?.FullName ?? "System";
            await exporter(dialog.FileName, _from, _to, generatedBy);
            MessageBoxHelper.ShowSuccess("Report exported successfully.", "Export Reports");

            if (MessageBoxHelper.ShowConfirmWarning("Open exported file?", "Export Reports"))
            {
                Process.Start(new ProcessStartInfo { FileName = dialog.FileName, UseShellExecute = true });
            }
        }
        catch (IOException)
        {
            MessageBoxHelper.ShowWarning("Unable to export. Please close the file if it is already open and try again.", "Export Reports");
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxHelper.ShowWarning("Unable to export. Please close the file if it is already open and try again.", "Export Reports");
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowError($"Unable to export report.\n\n{exception.Message}", "Export Reports");
        }
        finally
        {
            Cursor = Cursors.Default;
            sourceButton.Enabled = true;
        }
    }

    private string BuildExportFileName(string prefix, string extension)
    {
        string range = $"{_from:yyyy-MM-dd}_to_{_to:yyyy-MM-dd}";
        string safePrefix = new(prefix.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());
        safePrefix = safePrefix.Replace(' ', '_');
        if (!safePrefix.EndsWith("Reports_Bundle", StringComparison.Ordinal) &&
            !safePrefix.EndsWith("Report", StringComparison.Ordinal))
        {
            safePrefix += "_Report";
        }

        return $"Nataraki_{safePrefix}_{range}.{extension}";
    }
}
