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
            AddressDataSeeder.EnsureSeededAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                BuildStartupErrorMessage(exception),
                "Address Data Setup Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        try
        {
            DatabaseInitializer.ResetApplicationDataIfRequested();
            DatabaseInitializer.Initialize();
            AppBrandingManager.LoadSettingsAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            MessageBoxHelper.ShowDatabaseError(exception);
            return;
        }

        Application.Run(new LoginForm());
    }

    private static string BuildStartupErrorMessage(Exception exception)
    {
        string innerExceptionDetails = exception.InnerException is null
            ? "None"
            : exception.InnerException.ToString();

        return
            "The local Philippine address database could not be prepared during startup." +
            Environment.NewLine +
            Environment.NewLine +
            $"Message: {exception.Message}" +
            Environment.NewLine +
            Environment.NewLine +
            $"Inner exception: {innerExceptionDetails}" +
            Environment.NewLine +
            Environment.NewLine +
            $"Stack trace:{Environment.NewLine}{exception.StackTrace}";
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
