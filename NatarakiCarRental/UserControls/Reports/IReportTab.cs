namespace NatarakiCarRental.UserControls.Reports;

public interface IReportTab
{
    Task LoadAsync(DateTime from, DateTime to);
}
