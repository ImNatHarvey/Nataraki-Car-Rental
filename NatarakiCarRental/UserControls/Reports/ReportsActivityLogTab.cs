using FontAwesome.Sharp;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Models;
using NatarakiCarRental.Services;
using NatarakiCarRental.UserControls.Cards;

namespace NatarakiCarRental.UserControls.Reports;

public sealed class ReportsActivityLogTab : UserControl, IReportTab
{
    private readonly ReportService _reportService = new();
    private readonly FlowLayoutPanel _metricPanel = ReportLayoutHelper.CreateMetricPanel();

    private readonly MetricCardControl _totalLogsCard = new();
    private readonly MetricCardControl _criticalActionsCard = new();
    private readonly MetricCardControl _userManagementCard = new();
    private readonly MetricCardControl _financialActionsCard = new();
    private readonly MetricCardControl _distinctUsersCard = new();

    private readonly DataGridView _activityLogGrid = ReportLayoutHelper.CreateSummaryGrid();

    public ReportsActivityLogTab()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        ReportLayoutHelper.ConfigureReportPage(this);
        InitializeLayout();
        Resize += (_, _) => LayoutCards();
    }

    public async Task LoadAsync(DateTime from, DateTime to)
    {
        try
        {
            var metrics = await _reportService.GetAuditSummaryMetricsAsync(from, to);
            UpdateSummaryCards(metrics);
            
            var logs = await _reportService.GetActivityLogReportAsync(from, to);
            PopulateActivityLogs(logs);
            
            LayoutCards();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowWarning($"Unable to load audit reports.\n\n{exception.Message}", "Reports");
        }
    }

    private void InitializeLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4 };
        
        // 1. Summary
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("System Audit Summary"));
        layout.Controls.Add(_metricPanel);
        
        // 2. Details
        layout.Controls.Add(ReportLayoutHelper.CreateSectionHeader("Detailed System Activity Logs"));
        layout.Controls.Add(ReportLayoutHelper.CreateGridCard("Activity Log History", _activityLogGrid, 600));

        InitCards();
        Controls.Add(layout);
    }

    private void InitCards()
    {
        ReportLayoutHelper.AddMetricCard(_metricPanel, _totalLogsCard, IconChar.ClipboardList, "Total Logs", "0", "All system actions", ThemeHelper.Primary);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _criticalActionsCard, IconChar.TriangleExclamation, "Critical Actions", "0", "Archive/Delete/Blacklist", ThemeHelper.Danger);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _userManagementCard, IconChar.UserShield, "User Management", "0", "Security changes", ThemeHelper.Purple);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _financialActionsCard, IconChar.MoneyBillTrendUp, "Financial Actions", "0", "Payments & revenue", ThemeHelper.Success);
        ReportLayoutHelper.AddMetricCard(_metricPanel, _distinctUsersCard, IconChar.Users, "Active Users", "0", "Unique actors", ThemeHelper.Warning);
    }

    private void LayoutCards() => ReportLayoutHelper.LayoutMetricCards(_metricPanel, [_totalLogsCard, _criticalActionsCard, _userManagementCard, _financialActionsCard, _distinctUsersCard]);

    private void UpdateSummaryCards(AuditSummaryMetrics metrics)
    {
        _totalLogsCard.SetMetric(IconChar.ClipboardList, "Total Logs", metrics.TotalLogs.ToString(), "All system actions", ThemeHelper.Primary);
        _criticalActionsCard.SetMetric(IconChar.TriangleExclamation, "Critical Actions", metrics.CriticalActions.ToString(), "Archive/Delete/Blacklist", ThemeHelper.Danger);
        _userManagementCard.SetMetric(IconChar.UserShield, "User Management", metrics.UserManagementActions.ToString(), "Security changes", ThemeHelper.Purple);
        _financialActionsCard.SetMetric(IconChar.MoneyBillTrendUp, "Financial Actions", metrics.FinancialActions.ToString(), "Payments & revenue", ThemeHelper.Success);
        _distinctUsersCard.SetMetric(IconChar.Users, "Active Users", metrics.DistinctUsers.ToString(), "Unique actors", ThemeHelper.Warning);
    }

    private void PopulateActivityLogs(IReadOnlyList<ActivityLogReportItem> items)
    {
        _activityLogGrid.Columns.Clear(); _activityLogGrid.Rows.Clear();
        _activityLogGrid.Columns.Add("Date", "Timestamp");
        _activityLogGrid.Columns.Add("User", "User");
        _activityLogGrid.Columns.Add("Module", "Module");
        _activityLogGrid.Columns.Add("Action", "Action");
        _activityLogGrid.Columns.Add("Entity", "Entity");
        _activityLogGrid.Columns.Add("Description", "Description");

        if (_activityLogGrid.Columns["Date"] is DataGridViewColumn c1) c1.Width = 140;
        if (_activityLogGrid.Columns["User"] is DataGridViewColumn c2) c2.Width = 140;
        if (_activityLogGrid.Columns["Module"] is DataGridViewColumn c3) c3.Width = 100;
        if (_activityLogGrid.Columns["Action"] is DataGridViewColumn c4) c4.Width = 100;

        foreach (var item in items)
        {
            _activityLogGrid.Rows.Add(item.CreatedAt.ToString("MMM d, yyyy h:mm tt"), item.UserFullName, item.Module, item.Action, item.EntityName, item.Description);
        }
        ReportLayoutHelper.AddEmptyRow(_activityLogGrid);
    }
}