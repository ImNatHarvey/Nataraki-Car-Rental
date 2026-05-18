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
            DatabaseInitializer.Initialize();
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
}
