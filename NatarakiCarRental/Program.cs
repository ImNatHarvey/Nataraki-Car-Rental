using NatarakiCarRental.Data;
using NatarakiCarRental.Forms.Auth;
using NatarakiCarRental.Helpers;
using NatarakiCarRental.Tools;

namespace NatarakiCarRental;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            Task.Run(InitializeApplicationAsync).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                BuildStartupErrorMessage(exception),
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new LoginForm());
    }

    private static async Task InitializeApplicationAsync()
    {
        DatabaseAutoDiscovery.ResolveActiveServer();
        await AddressDataSeeder.EnsureSeededAsync();
        DatabaseInitializer.ResetApplicationDataIfRequested();
        DatabaseInitializer.Initialize();
        await AppBrandingManager.LoadSettingsAsync();
    }

    private static string BuildStartupErrorMessage(Exception exception)
    {
        return
            "Nataraki Car Rental could not complete startup initialization." +
            Environment.NewLine +
            Environment.NewLine +
            "Please check the database connection and local app data permissions, then try again." +
            Environment.NewLine +
            Environment.NewLine +
            $"Details: {exception.Message}";
    }

    private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
        MessageBoxHelper.ShowError($"An unexpected application error occurred.\n\n{e.Exception.Message}", "System Error");
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBoxHelper.ShowError($"A critical system error occurred.\n\n{ex.Message}", "Fatal Error");
        }
    }
}
