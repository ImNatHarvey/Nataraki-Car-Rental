using Microsoft.Data.SqlClient;
using NatarakiCarRental.Helpers;

namespace NatarakiCarRental.Data;

public static class DatabaseInitializer
{
    private const string BootstrapOwnerUsernameEnvironmentVariable = "NATARAKI_BOOTSTRAP_USERNAME";
    private const string BootstrapOwnerPasswordEnvironmentVariable = "NATARAKI_BOOTSTRAP_PASSWORD";
    private const string DefaultDemoBootstrapOwnerUsername = "NatarakiCar";
    private const string DefaultDemoBootstrapOwnerPassword = "Nataraki2026";

    public static void Initialize()
    {
        CreateDatabaseIfMissing();
        CreateTablesIfMissing();
        SeedRoles();
        SeedDefaultDemoOwner();
    }

    private static void CreateDatabaseIfMissing()
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM sys.databases
                WHERE name = N'NatarakiCarRentalDb'
            )
            BEGIN
                CREATE DATABASE NatarakiCarRentalDb;
            END;
            """;

        using SqlConnection connection = new(AppConstants.MasterConnectionString);
        connection.Open();

        using SqlCommand command = new(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void CreateTablesIfMissing()
    {
        string availableStatus = SqlLiteral(CarConstants.Status.Available);
        string validStatuses = SqlInList(CarConstants.Status.All);

        // 1. Roles Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Roles
                (
                    RoleId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    RoleName nvarchar(50) NOT NULL UNIQUE,
                    Description nvarchar(255) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime()
                );
            END;
            """);

        // 2. Users Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users
                (
                    UserId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    RoleId int NOT NULL,
                    Username nvarchar(50) NOT NULL UNIQUE,
                    PasswordHash nvarchar(255) NOT NULL,
                    FirstName nvarchar(100) NOT NULL,
                    LastName nvarchar(100) NOT NULL,
                    Email nvarchar(150) NULL,
                    PhoneNumber nvarchar(30) NULL,
                    IsActive bit NOT NULL DEFAULT 1,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    ArchivedAt datetime2 NULL,
                    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
                );
            END;
            """);

        // 3. Cars Table & Schema Updates
        ExecuteDatabaseCommand($$"""
            IF OBJECT_ID(N'dbo.Cars', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Cars
                (
                    CarId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CarName nvarchar(100) NOT NULL,
                    Brand nvarchar(100) NULL,
                    Model nvarchar(100) NOT NULL,
                    PlateNumber nvarchar(20) NOT NULL UNIQUE,
                    [Year] int NULL,
                    Color nvarchar(50) NULL,
                    Transmission nvarchar(50) NULL,
                    FuelType nvarchar(50) NULL,
                    SeatingCapacity int NULL,
                    RatePerDay decimal(18,2) NOT NULL,
                    Status nvarchar(30) NOT NULL DEFAULT {{availableStatus}},
                    CodingDay nvarchar(30) NULL,
                    Mileage int NULL,
                    RegistrationExpirationDate date NULL,
                    InsuranceExpirationDate date NULL,
                    ImagePath nvarchar(500) NULL,
                    OrCrPath nvarchar(500) NULL,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    ArchivedAt datetime2 NULL,
                    CONSTRAINT CK_Cars_RatePerDay_Positive CHECK (RatePerDay > 0),
                    CONSTRAINT CK_Cars_Mileage_NonNegative CHECK (Mileage IS NULL OR Mileage >= 0),
                    CONSTRAINT CK_Cars_SeatingCapacity_Positive CHECK (SeatingCapacity IS NULL OR SeatingCapacity > 0),
                    CONSTRAINT CK_Cars_Year_Valid CHECK ([Year] IS NULL OR [Year] BETWEEN 1000 AND 9999),
                    CONSTRAINT CK_Cars_Status_Valid CHECK (Status IN ({{validStatuses}}))
                );
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Cars', N'CodingDay') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars ADD CodingDay nvarchar(30) NULL;
                END;

                IF COL_LENGTH(N'dbo.Cars', N'Mileage') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars ADD Mileage int NULL;
                END;

                IF COL_LENGTH(N'dbo.Cars', N'RegistrationExpirationDate') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars ADD RegistrationExpirationDate date NULL;
                END;

                IF COL_LENGTH(N'dbo.Cars', N'InsuranceExpirationDate') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars ADD InsuranceExpirationDate date NULL;
                END;
            END;
            """);

        ExecuteDatabaseCommand($$"""
            IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'dbo.CK_Cars_RatePerDay_Positive', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars WITH CHECK
                    ADD CONSTRAINT CK_Cars_RatePerDay_Positive CHECK (RatePerDay > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Cars_Mileage_NonNegative', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars WITH CHECK
                    ADD CONSTRAINT CK_Cars_Mileage_NonNegative CHECK (Mileage IS NULL OR Mileage >= 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Cars_SeatingCapacity_Positive', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars WITH CHECK
                    ADD CONSTRAINT CK_Cars_SeatingCapacity_Positive CHECK (SeatingCapacity IS NULL OR SeatingCapacity > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Cars_Year_Valid', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars WITH CHECK
                    ADD CONSTRAINT CK_Cars_Year_Valid CHECK ([Year] IS NULL OR [Year] BETWEEN 1000 AND 9999);
                END;

                IF OBJECT_ID(N'dbo.CK_Cars_Status_Valid', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Cars WITH CHECK
                    ADD CONSTRAINT CK_Cars_Status_Valid CHECK (Status IN ({{validStatuses}}));
                END;
            END;
            """);

        // 4. Activity Logs
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.ActivityLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ActivityLogs
                (
                    ActivityLogId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NULL,
                    ActionType nvarchar(50) NOT NULL,
                    EntityName nvarchar(100) NULL,
                    EntityId int NULL,
                    Description nvarchar(500) NOT NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    CONSTRAINT FK_ActivityLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
                );
            END;
            """);

        // 5. Customers Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Customers
                (
                    CustomerId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    FirstName nvarchar(100) NOT NULL,
                    LastName nvarchar(100) NOT NULL,
                    Email nvarchar(150) NULL,
                    PhoneNumber nvarchar(30) NOT NULL,
                    Region nvarchar(150) NULL,
                    Province nvarchar(150) NULL,
                    City nvarchar(150) NULL,
                    Barangay nvarchar(150) NULL,
                    StreetAddress nvarchar(255) NULL,
                    IsBlacklisted bit NOT NULL DEFAULT 0,
                    BlacklistReason nvarchar(255) NULL,
                    IsArchived bit NOT NULL DEFAULT 0,
                    DriverLicensePath nvarchar(500) NULL,
                    ProofOfBillingPath nvarchar(500) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    ArchivedAt datetime2 NULL
                );
            END;
            """);

        // 6. Fleet Schedules
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.FleetSchedules', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.FleetSchedules
                (
                    ScheduleId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CarId int NOT NULL,
                    CustomerId int NULL,
                    Title nvarchar(150) NOT NULL,
                    ScheduleType nvarchar(30) NOT NULL,
                    Status nvarchar(30) NOT NULL,
                    StartDate date NOT NULL,
                    EndDate date NOT NULL,
                    Notes nvarchar(500) NULL,
                    CreatedByUserId int NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysutcdatetime(),
                    UpdatedAt datetime2 NULL,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CONSTRAINT FK_FleetSchedules_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars(CarId),
                    CONSTRAINT FK_FleetSchedules_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
                    CONSTRAINT FK_FleetSchedules_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
                );
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.FleetSchedules', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.FleetSchedules', N'CustomerId') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD CustomerId int NULL;
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'Title') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD Title nvarchar(150) NOT NULL CONSTRAINT DF_FleetSchedules_Title DEFAULT N'Untitled';
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'ScheduleType') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD ScheduleType nvarchar(30) NOT NULL CONSTRAINT DF_FleetSchedules_ScheduleType DEFAULT N'Reservation';
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'Status') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD Status nvarchar(30) NOT NULL CONSTRAINT DF_FleetSchedules_Status DEFAULT N'Pending';
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'StartDate') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD StartDate date NOT NULL CONSTRAINT DF_FleetSchedules_StartDate DEFAULT CONVERT(date, sysdatetime());
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'EndDate') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD EndDate date NOT NULL CONSTRAINT DF_FleetSchedules_EndDate DEFAULT CONVERT(date, sysdatetime());
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'Notes') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD Notes nvarchar(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'CreatedByUserId') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD CreatedByUserId int NULL;
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'CreatedAt') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD CreatedAt datetime2 NOT NULL CONSTRAINT DF_FleetSchedules_CreatedAt DEFAULT sysutcdatetime();
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD UpdatedAt datetime2 NULL;
                END;

                IF COL_LENGTH(N'dbo.FleetSchedules', N'IsArchived') IS NULL
                BEGIN
                    ALTER TABLE dbo.FleetSchedules ADD IsArchived bit NOT NULL CONSTRAINT DF_FleetSchedules_IsArchived DEFAULT 0;
                END;

                UPDATE dbo.FleetSchedules
                SET Status = CASE
                    WHEN ScheduleType = N'Maintenance' AND Status = N'Active' THEN N'Ongoing'
                    WHEN Status = N'Confirmed' THEN N'Reserved'
                    WHEN Status = N'Active' THEN N'Rented'
                    ELSE Status
                END
                WHERE Status IN (N'Confirmed', N'Active');
            END;
            """);

        // 7. Customers Schema Updates
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Customers', N'BlacklistReason') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD BlacklistReason nvarchar(255) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'Region') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD Region nvarchar(150) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'Province') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD Province nvarchar(150) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'City') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD City nvarchar(150) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'Barangay') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD Barangay nvarchar(150) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'StreetAddress') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD StreetAddress nvarchar(255) NULL;
                END;
            END;
            """);

        // 8. Customers Data Migration (Wrapped in sp_executesql to prevent parser errors)
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Customers', N'Address') IS NOT NULL 
                   AND COL_LENGTH(N'dbo.Customers', N'StreetAddress') IS NOT NULL
                BEGIN
                    EXEC sp_executesql N'
                        UPDATE dbo.Customers
                        SET StreetAddress = Address
                        WHERE StreetAddress IS NULL
                          AND Address IS NOT NULL
                          AND LEN(LTRIM(RTRIM(Address))) > 0;
                    ';
                END;
            END;
            """);

        // 9. Customers Constraints
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                UPDATE dbo.Customers
                SET BlacklistReason = N'Legacy blacklist record'
                WHERE IsBlacklisted = 1
                  AND (BlacklistReason IS NULL OR LEN(LTRIM(RTRIM(BlacklistReason))) = 0);

                UPDATE dbo.Customers
                SET BlacklistReason = NULL
                WHERE IsBlacklisted = 0
                  AND BlacklistReason IS NOT NULL;

                IF OBJECT_ID(N'dbo.UQ_Customers_PhoneNumber', N'UQ') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT UQ_Customers_PhoneNumber UNIQUE (PhoneNumber);
                END;

                IF OBJECT_ID(N'dbo.CK_Customers_FirstName_NotEmpty', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT CK_Customers_FirstName_NotEmpty CHECK (LEN(LTRIM(RTRIM(FirstName))) > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Customers_LastName_NotEmpty', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT CK_Customers_LastName_NotEmpty CHECK (LEN(LTRIM(RTRIM(LastName))) > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Customers_PhoneNumber_NotEmpty', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT CK_Customers_PhoneNumber_NotEmpty CHECK (LEN(LTRIM(RTRIM(PhoneNumber))) > 0);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE name = N'CK_Customers_BlacklistReason_Valid'
                      AND parent_object_id = OBJECT_ID(N'dbo.Customers')
                )
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT CK_Customers_BlacklistReason_Valid CHECK (
                        (IsBlacklisted = 0 AND BlacklistReason IS NULL)
                        OR (IsBlacklisted = 1 AND LEN(LTRIM(RTRIM(ISNULL(BlacklistReason, N'')))) > 0)
                    );
                END;

                IF OBJECT_ID(N'dbo.CK_Customers_ArchivedAt_Valid', N'C') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT CK_Customers_ArchivedAt_Valid CHECK (
                        (IsArchived = 0 AND ArchivedAt IS NULL)
                        OR (IsArchived = 1 AND ArchivedAt IS NOT NULL)
                    );
                END;
            END;
            """);

        // 10. Indexes
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Cars_IsArchived_CarId'
                      AND object_id = OBJECT_ID(N'dbo.Cars')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Cars_IsArchived_CarId
                ON dbo.Cars (IsArchived, CarId DESC);
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Cars_IsArchived_Status'
                      AND object_id = OBJECT_ID(N'dbo.Cars')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Cars_IsArchived_Status
                ON dbo.Cars (IsArchived, Status);
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.ActivityLogs', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ActivityLogs_CreatedAt'
                      AND object_id = OBJECT_ID(N'dbo.ActivityLogs')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_ActivityLogs_CreatedAt
                ON dbo.ActivityLogs (CreatedAt DESC);
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.FleetSchedules', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_FleetSchedules_CarId_DateRange'
                      AND object_id = OBJECT_ID(N'dbo.FleetSchedules')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_FleetSchedules_CarId_DateRange
                ON dbo.FleetSchedules (CarId, IsArchived, Status, StartDate, EndDate);
            END;
            """);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Customers_IsArchived_IsBlacklisted'
                      AND object_id = OBJECT_ID(N'dbo.Customers')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Customers_IsArchived_IsBlacklisted
                ON dbo.Customers (IsArchived, IsBlacklisted);
            END;
            """);
    }

    private static void SeedRoles()
    {
        InsertRoleIfMissing("Owner", "Full system owner access");
        InsertRoleIfMissing("Admin", "Administrative access");
        InsertRoleIfMissing("Manager", "Manages daily operations and reports");
        InsertRoleIfMissing("Agent", "Handles bookings and customer transactions");
        InsertRoleIfMissing("Staff", "Basic staff access");
    }

    private static void InsertRoleIfMissing(string roleName, string description)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName = @RoleName)
            BEGIN
                INSERT INTO dbo.Roles (RoleName, Description)
                VALUES (@RoleName, @Description);
            END;
            """;

        using SqlConnection connection = new(AppConstants.DefaultConnectionString);
        connection.Open();

        using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@RoleName", roleName);
        command.Parameters.AddWithValue("@Description", description);
        command.ExecuteNonQuery();
    }

    private static void SeedDefaultDemoOwner()
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @Username)
            BEGIN
                INSERT INTO dbo.Users
                (
                    RoleId,
                    Username,
                    PasswordHash,
                    FirstName,
                    LastName,
                    Email,
                    PhoneNumber
                )
                SELECT
                    RoleId,
                    @Username,
                    @PasswordHash,
                    @FirstName,
                    @LastName,
                    NULL,
                    NULL
                FROM dbo.Roles
                WHERE RoleName = N'Owner';
            END;
            """;

        // Demo/bootstrap credentials only. Override them through environment variables or appsettings.json outside demo use.
        string bootstrapUsername = GetBootstrapValue(
            BootstrapOwnerUsernameEnvironmentVariable,
            AppConfiguration.BootstrapOwnerUsername,
            DefaultDemoBootstrapOwnerUsername);
        string bootstrapPassword = GetBootstrapValue(
            BootstrapOwnerPasswordEnvironmentVariable,
            AppConfiguration.BootstrapOwnerPassword,
            DefaultDemoBootstrapOwnerPassword);
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapPassword);

        using SqlConnection connection = new(AppConstants.DefaultConnectionString);
        connection.Open();

        using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@Username", bootstrapUsername);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@FirstName", "System");
        command.Parameters.AddWithValue("@LastName", "Owner");
        command.ExecuteNonQuery();
    }

    private static void ExecuteDatabaseCommand(string sql)
    {
        using SqlConnection connection = new(AppConstants.DefaultConnectionString);
        connection.Open();

        using SqlCommand command = new(sql, connection);
        command.ExecuteNonQuery();
    }

    private static string SqlInList(IEnumerable<string> values)
    {
        return string.Join(", ", values.Select(SqlLiteral));
    }

    private static string SqlLiteral(string value)
    {
        return $"N'{value.Replace("'", "''")}'";
    }

    private static string GetBootstrapValue(string environmentVariable, string? configuredValue, string fallbackValue)
    {
        string? environmentValue = Environment.GetEnvironmentVariable(environmentVariable);

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue.Trim();
        }

        return string.IsNullOrWhiteSpace(configuredValue) ? fallbackValue : configuredValue.Trim();
    }
}
