using Microsoft.Data.SqlClient;
using NatarakiCarRental.Helpers;

namespace NatarakiCarRental.Data;

/// <summary>
/// Provides mechanism to automatically discover an active SQL Server instance on startup.
/// </summary>
public static class DatabaseAutoDiscovery
{
    /// <summary>
    /// Attempts to resolve an active SQL Server from a prioritized list and updates AppConstants.
    /// </summary>
    public static void ResolveActiveServer()
    {
        // Priority list as per requirements
        string?[] potentialServers =
        [
            AppConfiguration.DatabaseServer,
            Environment.MachineName,
            ".\\SQLEXPRESS",
            "localhost"
        ];

        foreach (string? server in potentialServers)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                continue;
            }

            if (TryPingServer(server))
            {
                AppConstants.SetResolvedServerName(server);
                return;
            }
        }
    }

    private static bool TryPingServer(string server)
    {
        // Use a very short timeout for the ping test as requested
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = server,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 2 // 2 seconds
        };

        try
        {
            // Attempt to connect to the master database (or just the server)
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            // Gracefully handle connection failures during discovery
            return false;
        }
        catch (Exception)
        {
            // Handle any other unexpected errors during ping
            return false;
        }
    }
}
