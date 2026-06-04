namespace NatarakiCarRental.Helpers;

public static class AppConstants
{
    public const string ApplicationName = "Nataraki Car Rental";
    public const string DatabaseName = "NatarakiCarRentalDb";
    private const string DefaultSqlServerName = "HARVEY";
    private const string ConnectionStringEnvironmentVariable = "NATARAKI_CONNECTION_STRING";
    private const string SqlServerEnvironmentVariable = "NATARAKI_SQL_SERVER";
    public const string UploadsFolder = "Uploads";
    public const string CarsUploadFolder = "Uploads\\Cars";
    public const string CustomersUploadFolder = "Uploads\\Customers";
    public const string PaymentsUploadFolder = "Uploads\\Payments";
    public const string OffsiteUploadFolder = "Uploads\\Offsite";
    public const string ProfileImagesUploadFolder = "Uploads\\Profiles";

    public static string MasterConnectionString => BuildConnectionString(includeDatabase: false);
    public static string DefaultConnectionString => BuildConnectionString(includeDatabase: true);

    private static string? _resolvedServerName;

    public static void SetResolvedServerName(string serverName)
    {
        _resolvedServerName = serverName;
    }

    private static string BuildConnectionString(bool includeDatabase)
    {
        string? configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        configuredConnectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? AppConfiguration.ConnectionString
            : configuredConnectionString;

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder = new(configuredConnectionString);

            if (includeDatabase)
            {
                builder.InitialCatalog = DatabaseName;
            }
            else
            {
                builder.InitialCatalog = string.Empty;
            }

            return builder.ConnectionString;
        }

        string serverName;
        if (!string.IsNullOrWhiteSpace(_resolvedServerName))
        {
            serverName = _resolvedServerName;
        }
        else
        {
            string? configuredServerName = Environment.GetEnvironmentVariable(SqlServerEnvironmentVariable);
            configuredServerName = string.IsNullOrWhiteSpace(configuredServerName)
                ? AppConfiguration.DatabaseServer
                : configuredServerName;
            serverName = string.IsNullOrWhiteSpace(configuredServerName)
                ? DefaultSqlServerName
                : configuredServerName.Trim();
        }

        Microsoft.Data.SqlClient.SqlConnectionStringBuilder fallbackBuilder = new()
        {
            DataSource = serverName,
            IntegratedSecurity = true,
            TrustServerCertificate = true
        };

        if (includeDatabase)
        {
            fallbackBuilder.InitialCatalog = DatabaseName;
        }

        return fallbackBuilder.ConnectionString;
    }
}
