using ClosedXML.Excel;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.Diagnostics;
using System.Drawing;

namespace NatarakiCarRental.Services;

public sealed class EnterpriseReportExportService
{
    static EnterpriseReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly ReportService _reportService = new();

    // Theme Color Helpers
    private static QuestPDF.Infrastructure.Color ThemePrimary => QuestPDF.Infrastructure.Color.FromRGB(ThemeHelper.Primary.R, ThemeHelper.Primary.G, ThemeHelper.Primary.B);
    private static QuestPDF.Infrastructure.Color ThemePrimaryDark => QuestPDF.Infrastructure.Color.FromRGB(ThemeHelper.PrimaryHover.R, ThemeHelper.PrimaryHover.G, ThemeHelper.PrimaryHover.B);
    private static QuestPDF.Infrastructure.Color ThemeContrastText => GetContrastTextColor(ThemeHelper.Primary);

    private static QuestPDF.Infrastructure.Color GetContrastTextColor(System.Drawing.Color bgColor)
    {
        // Calculate relative luminance
        double luminance = (0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B) / 255;
        return luminance > 0.6 ? Colors.Black : Colors.White;
    }

    public async Task ExportFinancialPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        var metrics = await _reportService.GetSummaryMetricsAsync(from, to);
        var profitability = await _reportService.GetOperatingProfitabilityAsync(from, to);
        var paymentMethods = await _reportService.GetPaymentMethodBreakdownAsync(from, to);
        var topCars = await _reportService.GetRevenueByCarAsync(from, to, 10);

        var kpis = new Dictionary<string, string>
        {
            ["Total Revenue"] = FormattingHelper.FormatPeso(metrics.TotalRevenue),
            ["Outstanding"] = FormattingHelper.FormatPeso(metrics.OutstandingBalance),
            ["Net Profit"] = FormattingHelper.FormatPeso(profitability.NetAfterOffsiteCost),
            ["Cost Ratio"] = $"{profitability.CostToRevenueRatio:N1}%"
        };

        await ExportToPdfAsync(path, "Financial Summary Report", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            ComposeKpiSection(column, kpis);
            
            column.Item().PaddingTop(10).Text("Revenue by Payment Method").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, paymentMethods);

            column.Item().PaddingTop(15).Text("Top Earning Vehicles").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, topCars);
        });
    }

    public async Task ExportFleetPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        var metrics = await _reportService.GetFleetPerformanceMetricsAsync(from, to);
        var utilization = await _reportService.GetFleetUtilizationAsync(from, to);
        var revenuePerCar = await _reportService.GetFleetRevenuePerCarAsync(from, to);

        var kpis = new Dictionary<string, string>
        {
            ["Total Fleet Revenue"] = FormattingHelper.FormatPeso(metrics.TotalFleetRevenue),
            ["Avg Utilization"] = $"{metrics.AverageUtilizationRate:N1}%",
            ["Active Rentals"] = metrics.ActiveRentals.ToString(),
            ["Maintenance"] = metrics.CarsUnderMaintenance.ToString()
        };

        await ExportToPdfAsync(path, "Fleet Performance Report", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            ComposeKpiSection(column, kpis);
            
            column.Item().PaddingTop(10).Text("Vehicle Utilization Details").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, utilization);

            column.Item().PaddingTop(15).Text("Revenue Breakdown Per Vehicle").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, revenuePerCar);
        });
    }

    public async Task ExportOperationsPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        var metrics = await _reportService.GetOperationsMetricsAsync(from, to);
        var upcomingReturns = await _reportService.GetUpcomingReturnsAsync(from, to);
        var lateReturns = await _reportService.GetLateReturnsAsync(DateTime.Today);
        var reservations = await _reportService.GetUpcomingReservationsAsync(from, to);

        var kpis = new Dictionary<string, string>
        {
            ["Upcoming Returns"] = metrics.UpcomingReturns.ToString(),
            ["Late Returns"] = metrics.LateReturns.ToString(),
            ["Upcoming Res."] = metrics.UpcomingReservations.ToString(),
            ["Available Cars"] = metrics.AvailableCars.ToString()
        };

        await ExportToPdfAsync(path, "Operations Activity Report", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            ComposeKpiSection(column, kpis);
            
            column.Item().PaddingTop(10).Text("Upcoming Returns").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, upcomingReturns);

            column.Item().PaddingTop(15).Text("Late Returns (Action Required)").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, lateReturns);

            column.Item().PaddingTop(15).Text("Upcoming Reservations").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, reservations);
        });
    }

    public async Task ExportCustomerPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        var metrics = await _reportService.GetCustomerAnalyticsMetricsAsync(from, to);
        var topRevenue = await _reportService.GetTopCustomersByRevenueAsync(from, to, 15);
        var outstanding = await _reportService.GetCustomersWithOutstandingBalancesAsync(from, to);
        var blacklisted = await _reportService.GetBlacklistedCustomersReportAsync(from, to);

        var kpis = new Dictionary<string, string>
        {
            ["Active Customers"] = metrics.TotalActiveCustomers.ToString(),
            ["New This Period"] = metrics.NewCustomers.ToString(),
            ["Late Returners"] = metrics.CustomersWithLateReturns.ToString(),
            ["Blacklisted"] = metrics.BlacklistedCustomers.ToString()
        };

        await ExportToPdfAsync(path, "Customer Analytics Report", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            ComposeKpiSection(column, kpis);
            
            column.Item().PaddingTop(10).Text("Top Customers by Revenue").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, topRevenue);

            column.Item().PaddingTop(15).Text("Customers with Outstanding Balances").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, outstanding);

            column.Item().PaddingTop(15).Text("Blacklisted Customer Registry").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, blacklisted);
        });
    }

    public async Task ExportAuditPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        var metrics = await _reportService.GetAuditSummaryMetricsAsync(from, to);
        var logs = await _reportService.GetActivityLogReportAsync(from, to);

        var kpis = new Dictionary<string, string>
        {
            ["Total Logs"] = metrics.TotalLogs.ToString(),
            ["Critical Actions"] = metrics.CriticalActions.ToString(),
            ["Security Changes"] = metrics.UserManagementActions.ToString(),
            ["Active Users"] = metrics.DistinctUsers.ToString()
        };

        await ExportToPdfAsync(path, "System Audit & Activity Report", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            ComposeKpiSection(column, kpis);
            
            column.Item().PaddingTop(10).Text("Detailed Activity Log").FontSize(12).SemiBold().FontColor(ThemePrimaryDark);
            ComposeTable(column, logs);
        });
    }

    public async Task ExportFullPdfAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        // PRE-FETCH ALL DATA HERE (Outside QuestPDF composition)
        var topCars = await _reportService.GetRevenueByCarAsync(from, to, 10);
        var utilization = await _reportService.GetFleetUtilizationAsync(from, to);
        var lateReturns = await _reportService.GetLateReturnsAsync(DateTime.Today);
        var topCustomers = await _reportService.GetTopCustomersByRevenueAsync(from, to, 10);

        await ExportToPdfAsync(path, "Full Enterprise Report Bundle", generatedBy, $"Period: {from:MMM d} to {to:MMM d, yyyy}", column =>
        {
            column.Item().Text("Note: This bundle contains Financial, Fleet, Operations, and Customer summaries.").FontSize(10).Italic();
            
            // Financial Summary
            column.Item().PaddingTop(20).Text("FINANCIAL SUMMARY").FontSize(14).Bold().FontColor(ThemePrimaryDark);
            column.Item().PaddingTop(10).Text("Top Cars by Revenue").FontSize(10).SemiBold().FontColor(ThemePrimary);
            ComposeTable(column, topCars);

            // Fleet Summary
            column.Item().PageBreak();
            column.Item().PaddingTop(10).Text("FLEET PERFORMANCE").FontSize(14).Bold().FontColor(ThemePrimaryDark);
            column.Item().PaddingTop(10).Text("Vehicle Utilization").FontSize(10).SemiBold().FontColor(ThemePrimary);
            ComposeTable(column, utilization);

            // Operations Summary
            column.Item().PageBreak();
            column.Item().PaddingTop(10).Text("OPERATIONS OVERVIEW").FontSize(14).Bold().FontColor(ThemePrimaryDark);
            column.Item().PaddingTop(10).Text("Late Returns").FontSize(10).SemiBold().FontColor(ThemePrimary);
            ComposeTable(column, lateReturns);

            // Customer Summary
            column.Item().PageBreak();
            column.Item().PaddingTop(10).Text("CUSTOMER ANALYTICS").FontSize(14).Bold().FontColor(ThemePrimaryDark);
            column.Item().PaddingTop(10).Text("Top Customers").FontSize(10).SemiBold().FontColor(ThemePrimary);
            ComposeTable(column, topCustomers);
        });
    }

    public async Task ExportFullExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Financial", await _reportService.GetPaymentMethodBreakdownAsync(from, to), "Financial Overview", from, to, generatedBy);
        AddSheet(workbook, "Fleet", await _reportService.GetFleetUtilizationAsync(from, to), "Fleet Overview", from, to, generatedBy);
        AddSheet(workbook, "Operations", await _reportService.GetUpcomingReturnsAsync(from, to), "Operations Overview", from, to, generatedBy);
        AddSheet(workbook, "Customers", await _reportService.GetTopCustomersByRevenueAsync(from, to, 100), "Customer Overview", from, to, generatedBy);
        
        await Task.Run(() => workbook.SaveAs(path));
    }

    public async Task ExportFinancialExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Payment Methods", await _reportService.GetPaymentMethodBreakdownAsync(from, to), "Financial Report", from, to, generatedBy);
        AddSheet(workbook, "Top Cars", await _reportService.GetRevenueByCarAsync(from, to, 50), "Financial Report", from, to, generatedBy);
        await Task.Run(() => workbook.SaveAs(path));
    }

    public async Task ExportFleetExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Utilization", await _reportService.GetFleetUtilizationAsync(from, to), "Fleet Report", from, to, generatedBy);
        AddSheet(workbook, "Revenue Per Car", await _reportService.GetFleetRevenuePerCarAsync(from, to), "Fleet Report", from, to, generatedBy);
        await Task.Run(() => workbook.SaveAs(path));
    }

    public async Task ExportOperationsExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Upcoming Returns", await _reportService.GetUpcomingReturnsAsync(from, to), "Operations Report", from, to, generatedBy);
        AddSheet(workbook, "Late Returns", await _reportService.GetLateReturnsAsync(DateTime.Today), "Operations Report", from, to, generatedBy);
        AddSheet(workbook, "Reservations", await _reportService.GetUpcomingReservationsAsync(from, to), "Operations Report", from, to, generatedBy);
        await Task.Run(() => workbook.SaveAs(path));
    }

    public async Task ExportCustomerExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Top Revenue", await _reportService.GetTopCustomersByRevenueAsync(from, to, 100), "Customer Report", from, to, generatedBy);
        AddSheet(workbook, "Outstanding", await _reportService.GetCustomersWithOutstandingBalancesAsync(from, to), "Customer Report", from, to, generatedBy);
        AddSheet(workbook, "Blacklisted", await _reportService.GetBlacklistedCustomersReportAsync(from, to), "Customer Report", from, to, generatedBy);
        await Task.Run(() => workbook.SaveAs(path));
    }

    public async Task ExportAuditExcelAsync(string path, DateTime from, DateTime to, string generatedBy)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Activity Log", await _reportService.GetActivityLogReportAsync(from, to), "Audit Log Report", from, to, generatedBy);
        await Task.Run(() => workbook.SaveAs(path));
    }

    private void AddSheet<T>(XLWorkbook workbook, string sheetName, IEnumerable<T> items, string title, DateTime from, DateTime to, string generatedBy)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        ws.Cell(1, 1).Value = $"{title} - {sheetName}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd} | Generated By: {generatedBy}";
        
        ws.Cell(4, 1).InsertTable(items);
        ws.Columns().AdjustToContents();
    }

    private async Task ExportToPdfAsync(string path, string title, string generatedBy, string filterSummary, Action<ColumnDescriptor> composeContent)
    {
        await Task.Run(() =>
        {
            try
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text(AppBrandingManager.CurrentSettings.BusinessName).FontSize(16).SemiBold().FontColor(ThemePrimary);
                                col.Item().Text(AppBrandingManager.CurrentSettings.BusinessAddress).FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            row.RelativeItem().AlignRight().Column(col =>
                            {
                                col.Item().Text(title).FontSize(20).ExtraBold().FontColor(ThemePrimaryDark);
                                col.Item().Text($"Generated By: {generatedBy} | Date: {DateTime.Now:MMM d, yyyy h:mm tt}").FontSize(8);
                                col.Item().Text(filterSummary).FontSize(8).Italic().FontColor(Colors.Grey.Medium);
                            });
                        });

                        page.Content().PaddingTop(10).Column(composeContent);

                        page.Footer().PaddingTop(10).BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).Row(row =>
                        {
                            row.RelativeItem().Text(x =>
                            {
                                x.Span("Confidential Report - ").FontSize(8).FontColor(Colors.Grey.Medium);
                                x.Span(AppBrandingManager.CurrentSettings.BusinessName).FontSize(8).FontColor(Colors.Grey.Medium);
                            });

                            row.RelativeItem().AlignRight().Text(x =>
                            {
                                x.Span("Page ").FontSize(8);
                                x.CurrentPageNumber().FontSize(8);
                                x.Span(" of ").FontSize(8);
                                x.TotalPages().FontSize(8);
                            });
                        });
                    });
                });

                document.GeneratePdf(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"QuestPDF failed to generate document: {ex.Message}", ex);
            }
        });
    }

    private void ComposeKpiSection(ColumnDescriptor column, Dictionary<string, string> kpis)
    {
        column.Item().PaddingBottom(10).Row(row =>
        {
            foreach (var kpi in kpis)
            {
                row.RelativeItem().Padding(4).Border(0.5f).BorderColor(Colors.Grey.Lighten2).BorderTop(2).BorderColor(ThemePrimary).Background(Colors.Grey.Lighten4).Column(c =>
                {
                    c.Item().AlignCenter().Text(kpi.Key).FontSize(7).SemiBold().FontColor(Colors.Grey.Medium);
                    c.Item().AlignCenter().Text(kpi.Value).FontSize(11).Bold().FontColor(ThemePrimary);
                });
            }
        });
    }

    private void ComposeTable<T>(ColumnDescriptor column, IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            column.Item().Padding(10).AlignCenter().Text("No records found for the selected period.").Italic().FontColor(Colors.Grey.Medium);
            return;
        }

        column.Item().Table(table =>
        {
            var properties = typeof(T).GetProperties()
                .Where(p => p.PropertyType == typeof(string) || 
                            p.PropertyType == typeof(decimal) || 
                            p.PropertyType == typeof(int) || 
                            p.PropertyType == typeof(DateTime) || 
                            p.PropertyType == typeof(DateTime?))
                .ToList();
            
            table.ColumnsDefinition(columns =>
            {
                foreach (var prop in properties) columns.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var prop in properties)
                {
                    header.Cell().Background(ThemePrimaryDark).Padding(4).Text(FormattingHelper.SplitCamelCase(prop.Name)).FontColor(ThemeContrastText).SemiBold().FontSize(8);
                }
            });

            int rowIdx = 0;
            foreach (var item in items)
            {
                foreach (var prop in properties)
                {
                    var val = prop.GetValue(item);
                    string text = val switch
                    {
                        decimal d => FormattingHelper.FormatPeso(d),
                        DateTime dt => dt.ToString("MMM d, yyyy"),
                        _ => val?.ToString() ?? "-"
                    };

                    // Create the cell with a single fluent chain to avoid reusing container descriptors
                    if (rowIdx % 2 == 1)
                    {
                        table.Cell()
                             .BorderBottom(0.5f)
                             .BorderColor(Colors.Grey.Lighten3)
                             .Padding(4)
                             .Background(Colors.Grey.Lighten5)
                             .Text(text)
                             .FontSize(8);
                    }
                    else
                    {
                        table.Cell()
                             .BorderBottom(0.5f)
                             .BorderColor(Colors.Grey.Lighten3)
                             .Padding(4)
                             .Text(text)
                             .FontSize(8);
                    }
                }
                rowIdx++;
            }
        });
    }
}