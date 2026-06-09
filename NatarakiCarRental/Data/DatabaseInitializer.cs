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

        using SqlConnection connection = new(AppConstants.DefaultConnectionString);
        connection.Open();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            CreateTablesIfMissing(connection, transaction);
            SeedRoles(connection, transaction);
            SeedPermissions(connection, transaction);
            SeedRolePermissions(connection, transaction);
            SeedDefaultDemoOwner(connection, transaction);
            NormalizeOwnerUserFlags(connection, transaction);
            RepairInvalidOwnerPasswordHash(connection, transaction);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static void ResetApplicationDataIfRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("NATARAKI_RESET_DATABASE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        CreateDatabaseIfMissing();

        using SqlConnection connection = new(AppConstants.DefaultConnectionString);
        connection.Open();
        using SqlTransaction transaction = connection.BeginTransaction();

        try
        {
            ExecuteDatabaseCommand("""
                IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NOT NULL DELETE FROM dbo.TransactionPayments;
                IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL DELETE FROM dbo.Transactions;
                IF OBJECT_ID(N'dbo.FleetSchedules', N'U') IS NOT NULL DELETE FROM dbo.FleetSchedules;
                IF OBJECT_ID(N'dbo.ActivityLogs', N'U') IS NOT NULL DELETE FROM dbo.ActivityLogs;
                IF OBJECT_ID(N'dbo.Cars', N'U') IS NOT NULL DELETE FROM dbo.Cars;
                IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL DELETE FROM dbo.Customers;
                IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DELETE FROM dbo.Users;
                IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL DELETE FROM dbo.Roles;
                """, connection, transaction);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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

    private static void CreateTablesIfMissing(SqlConnection connection, SqlTransaction transaction)
    {
        // 1. Roles Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Roles
                (
                    RoleId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    RoleName nvarchar(100) NOT NULL UNIQUE,
                    Description nvarchar(300) NULL,
                    IsSystemRole bit NOT NULL DEFAULT 0,
                    IsActive bit NOT NULL DEFAULT 1,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Roles', N'IsSystemRole') IS NULL
                BEGIN
                    ALTER TABLE dbo.Roles ADD IsSystemRole bit NOT NULL DEFAULT 0;
                END;
                IF COL_LENGTH(N'dbo.Roles', N'IsActive') IS NULL
                BEGIN
                    ALTER TABLE dbo.Roles ADD IsActive bit NOT NULL DEFAULT 1;
                END;
                IF COL_LENGTH(N'dbo.Roles', N'IsArchived') IS NULL
                BEGIN
                    ALTER TABLE dbo.Roles ADD IsArchived bit NOT NULL DEFAULT 0;
                END;
                IF COL_LENGTH(N'dbo.Roles', N'UpdatedAt') IS NULL
                BEGIN
                    ALTER TABLE dbo.Roles ADD UpdatedAt datetime2 NULL;
                END;
            END;
            """, connection, transaction);

        // 2. Users Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users
                (
                    UserId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    RoleId int NOT NULL,
                    Username nvarchar(100) NOT NULL UNIQUE,
                    PasswordHash nvarchar(max) NOT NULL,
                    FirstName nvarchar(100) NOT NULL,
                    LastName nvarchar(100) NOT NULL,
                    Email nvarchar(150) NULL,
                    PhoneNumber nvarchar(30) NULL,
                    ProfileImagePath nvarchar(500) NULL,
                    SecurityQuestion nvarchar(300) NULL,
                    SecurityAnswer nvarchar(300) NULL,
                    IsActive bit NOT NULL DEFAULT 1,
                    IsOwner bit NOT NULL DEFAULT 0,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    LastLoginAt datetime2 NULL,
                    ArchivedAt datetime2 NULL,
                    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Users', N'SecurityQuestion') IS NULL
                BEGIN
                    ALTER TABLE dbo.Users ADD SecurityQuestion nvarchar(300) NULL;
                END;
                IF COL_LENGTH(N'dbo.Users', N'SecurityAnswer') IS NULL
                BEGIN
                    ALTER TABLE dbo.Users ADD SecurityAnswer nvarchar(300) NULL;
                END;
                IF COL_LENGTH(N'dbo.Users', N'IsOwner') IS NULL
                BEGIN
                    ALTER TABLE dbo.Users ADD IsOwner bit NOT NULL DEFAULT 0;
                END;
                IF COL_LENGTH(N'dbo.Users', N'LastLoginAt') IS NULL
                BEGIN
                    ALTER TABLE dbo.Users ADD LastLoginAt datetime2 NULL;
                END;
                IF COL_LENGTH(N'dbo.Users', N'ProfileImagePath') IS NULL
                BEGIN
                    ALTER TABLE dbo.Users ADD ProfileImagePath nvarchar(500) NULL;
                END;
            END;
            """, connection, transaction);

        // 2.1 Permissions Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Permissions
                (
                    PermissionId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PermissionKey nvarchar(150) NOT NULL UNIQUE,
                    PermissionName nvarchar(150) NOT NULL,
                    ModuleName nvarchar(100) NOT NULL,
                    Description nvarchar(300) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime()
                );
            END;
            """, connection, transaction);

        // 2.2 RolePermissions Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RolePermissions
                (
                    RoleId int NOT NULL,
                    PermissionId int NOT NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
                    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
                    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(PermissionId)
                );
            END;
            """, connection, transaction);

        // 3. Cars Table & Schema Updates
        ExecuteDatabaseCommand("""
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
                    Status nvarchar(30) NOT NULL DEFAULT N'Available',
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
                    CONSTRAINT CK_Cars_Status_Valid CHECK (Status IN (N'Available', N'Rented', N'Maintenance'))
                );
            END;
            """, connection, transaction);

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
            """, connection, transaction);

        ExecuteDatabaseCommand("""
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
                    ADD CONSTRAINT CK_Cars_Status_Valid CHECK (Status IN (N'Available', N'Rented', N'Maintenance'));
                END;
            END;
            """, connection, transaction);

        // 4. Vehicle Locations
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.VehicleLocations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.VehicleLocations
                (
                    VehicleLocationId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CarId int NOT NULL,
                    Latitude decimal(10,7) NOT NULL,
                    Longitude decimal(10,7) NOT NULL,
                    SpeedKph decimal(10,2) NULL,
                    Heading decimal(10,2) NULL,
                    Source nvarchar(50) NOT NULL CONSTRAINT DF_VehicleLocations_Source DEFAULT N'Simulator',
                    RecordedAt datetime2 NOT NULL CONSTRAINT DF_VehicleLocations_RecordedAt DEFAULT sysdatetime(),
                    IsArchived bit NOT NULL CONSTRAINT DF_VehicleLocations_IsArchived DEFAULT 0,
                    CONSTRAINT FK_VehicleLocations_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars(CarId),
                    CONSTRAINT CK_VehicleLocations_Latitude CHECK (Latitude BETWEEN -90 AND 90),
                    CONSTRAINT CK_VehicleLocations_Longitude CHECK (Longitude BETWEEN -180 AND 180)
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.VehicleLocations', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_VehicleLocations_CarId_RecordedAt'
                      AND object_id = OBJECT_ID(N'dbo.VehicleLocations')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_VehicleLocations_CarId_RecordedAt
                    ON dbo.VehicleLocations (CarId, RecordedAt DESC);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_VehicleLocations_Active'
                      AND object_id = OBJECT_ID(N'dbo.VehicleLocations')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_VehicleLocations_Active
                    ON dbo.VehicleLocations (CarId, RecordedAt DESC)
                    WHERE IsArchived = 0;
                END;
            END;
            """, connection, transaction);

        // 5. Activity Logs
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.ActivityLogs', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ActivityLogs
                (
                    ActivityLogId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NULL,
                    UserFullName nvarchar(255) NULL,
                    Module nvarchar(100) NULL,
                    Action nvarchar(50) NOT NULL,
                    EntityName nvarchar(100) NULL,
                    EntityId int NULL,
                    Description nvarchar(500) NOT NULL,
                    OldValue nvarchar(max) NULL,
                    NewValue nvarchar(max) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    CONSTRAINT FK_ActivityLogs_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
                );
            END;
            ELSE
            BEGIN
                IF COL_LENGTH(N'dbo.ActivityLogs', N'Action') IS NULL
                BEGIN
                    ALTER TABLE dbo.ActivityLogs ADD UserFullName nvarchar(255) NULL;
                    ALTER TABLE dbo.ActivityLogs ADD Module nvarchar(100) NULL;
                    ALTER TABLE dbo.ActivityLogs ADD Action nvarchar(50) NULL;
                    ALTER TABLE dbo.ActivityLogs ADD OldValue nvarchar(max) NULL;
                    ALTER TABLE dbo.ActivityLogs ADD NewValue nvarchar(max) NULL;
                    
                    EXEC('UPDATE dbo.ActivityLogs SET 
                        Action = ActionType, 
                        Module = EntityName,
                        UserFullName = (SELECT TOP 1 LTRIM(RTRIM(CONCAT(FirstName, '' '', LastName))) FROM dbo.Users WHERE UserId = dbo.ActivityLogs.UserId)');
                    
                    ALTER TABLE dbo.ActivityLogs ALTER COLUMN Action nvarchar(50) NOT NULL;
                END;
            END;
            """, connection, transaction);

        // 5. Customers Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Customers
                (
                    CustomerId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CustomerType nvarchar(50) NOT NULL DEFAULT N'Rental',
                    FirstName nvarchar(100) NULL,
                    LastName nvarchar(100) NULL,
                    CompanyName nvarchar(200) NULL,
                    ContactPerson nvarchar(150) NULL,
                    Email nvarchar(150) NULL,
                    PhoneNumber nvarchar(30) NOT NULL,
                    Region nvarchar(150) NULL,
                    Province nvarchar(150) NULL,
                    City nvarchar(150) NULL,
                    Barangay nvarchar(150) NULL,
                    StreetAddress nvarchar(255) NULL,
                    IsBlacklisted bit NOT NULL DEFAULT 0,
                    BlacklistReason nvarchar(255) NULL,
                    IsWalkIn bit NOT NULL DEFAULT 0,
                    IsArchived bit NOT NULL DEFAULT 0,
                    Notes nvarchar(max) NULL,
                    MaintenancePreferences nvarchar(max) NULL,
                    DriverLicensePath nvarchar(500) NULL,
                    ProofOfBillingPath nvarchar(500) NULL,
                    ValidIdFilePath nvarchar(500) NULL,
                    SelfieWithValidIdFilePath nvarchar(500) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    ArchivedAt datetime2 NULL
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Customers', N'CustomerType') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD CustomerType nvarchar(50) NOT NULL DEFAULT N'Rental';
                END;
                IF COL_LENGTH(N'dbo.Customers', N'CompanyName') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD CompanyName nvarchar(200) NULL;
                END;
                IF COL_LENGTH(N'dbo.Customers', N'ContactPerson') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD ContactPerson nvarchar(150) NULL;
                END;
                IF COL_LENGTH(N'dbo.Customers', N'Notes') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD Notes nvarchar(max) NULL;
                END;
                IF COL_LENGTH(N'dbo.Customers', N'MaintenancePreferences') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD MaintenancePreferences nvarchar(max) NULL;
                END;
            END;
            """, connection, transaction);

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
            """, connection, transaction);

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
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.FleetSchedules', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'dbo.CK_FleetSchedules_DateRange_Valid', N'C') IS NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.FleetSchedules
                        WHERE StartDate > EndDate
                   )
                BEGIN
                    ALTER TABLE dbo.FleetSchedules WITH CHECK
                    ADD CONSTRAINT CK_FleetSchedules_DateRange_Valid CHECK (StartDate <= EndDate);
                END;

                IF OBJECT_ID(N'dbo.CK_FleetSchedules_ScheduleType_Valid', N'C') IS NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.FleetSchedules
                        WHERE ScheduleType NOT IN (N'Reservation', N'Rental', N'Maintenance')
                   )
                BEGIN
                    ALTER TABLE dbo.FleetSchedules WITH CHECK
                    ADD CONSTRAINT CK_FleetSchedules_ScheduleType_Valid CHECK (
                        ScheduleType IN (N'Reservation', N'Rental', N'Maintenance')
                    );
                END;

                IF OBJECT_ID(N'dbo.CK_FleetSchedules_Status_Valid', N'C') IS NOT NULL
                    ALTER TABLE dbo.FleetSchedules DROP CONSTRAINT CK_FleetSchedules_Status_Valid;

                IF OBJECT_ID(N'dbo.CK_FleetSchedules_TypeStatus_Valid', N'C') IS NOT NULL
                    ALTER TABLE dbo.FleetSchedules DROP CONSTRAINT CK_FleetSchedules_TypeStatus_Valid;

                UPDATE dbo.FleetSchedules
                SET Status = CASE
                    WHEN ScheduleType = N'Maintenance' AND Status = N'Ongoing' THEN N'Maintenance'
                    WHEN ScheduleType = N'Rental' AND Status = N'Ongoing' THEN N'Rented'
                    WHEN ScheduleType = N'Rental' AND Status = N'Active' THEN N'Rented'
                    WHEN ScheduleType = N'Reservation' AND Status = N'Ongoing' THEN N'Reserved'
                    WHEN ScheduleType = N'Reservation' AND Status = N'Active' THEN N'Reserved'
                    ELSE Status
                END
                WHERE Status IN (N'Ongoing', N'Active');

                ALTER TABLE dbo.FleetSchedules WITH CHECK
                ADD CONSTRAINT CK_FleetSchedules_Status_Valid CHECK (
                    Status IN (N'Pending', N'Reserved', N'Rented', N'Ongoing', N'Maintenance', N'Completed', N'Cancelled')
                );

                ALTER TABLE dbo.FleetSchedules WITH CHECK
                ADD CONSTRAINT CK_FleetSchedules_TypeStatus_Valid CHECK (
                    (ScheduleType = N'Reservation' AND Status IN (N'Pending', N'Reserved', N'Cancelled'))
                    OR (ScheduleType = N'Rental' AND Status IN (N'Rented', N'Completed', N'Cancelled'))
                    OR (ScheduleType = N'Maintenance' AND Status IN (N'Pending', N'Maintenance', N'Completed', N'Cancelled'))
                );
            END;
            """, connection, transaction);

        // 7. Transactions
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Transactions
                (
                    TransactionId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TransactionType nvarchar(50) NOT NULL DEFAULT N'Rental',
                    MaintenanceType nvarchar(50) NULL,
                    TransactionCode nvarchar(40) NOT NULL,
                    FleetScheduleId int NOT NULL,
                    CustomerId int NOT NULL,
                    CarId int NOT NULL,
                    StartDate date NOT NULL,
                    EndDate date NOT NULL,
                    DailyRate decimal(18,2) NOT NULL,
                    TotalDays int NOT NULL,
                    TotalAmount decimal(18,2) NOT NULL,
                    AmountPaid decimal(18,2) NOT NULL DEFAULT 0,
                    BalanceAmount decimal(18,2) NOT NULL DEFAULT 0,
                    ModeOfPayment nvarchar(30) NOT NULL,
                    PaymentStatus nvarchar(30) NOT NULL,
                    TransactionStatus nvarchar(30) NOT NULL,
                    Notes nvarchar(500) NULL,
                    CreatedByUserId int NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    ArchivedAt datetime2 NULL,
                    IsArchived bit NOT NULL DEFAULT 0,
                    CONSTRAINT UQ_Transactions_TransactionCode UNIQUE (TransactionCode),
                    CONSTRAINT FK_Transactions_FleetSchedules FOREIGN KEY (FleetScheduleId) REFERENCES dbo.FleetSchedules(ScheduleId),
                    CONSTRAINT FK_Transactions_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(CustomerId),
                    CONSTRAINT FK_Transactions_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars(CarId),
                    CONSTRAINT FK_Transactions_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT CK_Transactions_DateRange_Valid CHECK (StartDate <= EndDate),
                    CONSTRAINT CK_Transactions_DailyRate_Positive CHECK (DailyRate > 0),
                    CONSTRAINT CK_Transactions_TotalDays_Positive CHECK (TotalDays > 0),
                    CONSTRAINT CK_Transactions_TotalAmount_NonNegative CHECK (TotalAmount >= 0),
                    CONSTRAINT CK_Transactions_AmountPaid_Valid CHECK (AmountPaid >= 0 AND AmountPaid <= TotalAmount),
                    CONSTRAINT CK_Transactions_BalanceAmount_Valid CHECK (BalanceAmount >= 0 AND BalanceAmount = TotalAmount - AmountPaid),
                    CONSTRAINT CK_Transactions_ModeOfPayment_Valid CHECK (ModeOfPayment IN (N'Cash', N'GCash', N'Bank Transfer', N'Other')),
                    CONSTRAINT CK_Transactions_PaymentStatus_Valid CHECK (PaymentStatus IN (N'Unpaid', N'Partial', N'Paid')),
                    CONSTRAINT CK_Transactions_Status_Valid CHECK (TransactionStatus IN (N'Pending', N'Reserved', N'Active', N'Maintenance', N'Completed', N'Cancelled'))
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Transactions', N'TransactionType') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD TransactionType nvarchar(50) NOT NULL DEFAULT N'Rental';
                END;

                IF COL_LENGTH(N'dbo.Transactions', N'MaintenanceType') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD MaintenanceType nvarchar(50) NULL;
                END;
                
                -- Update Status constraint
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Transactions_Status_Valid')
                BEGIN
                    ALTER TABLE dbo.Transactions DROP CONSTRAINT CK_Transactions_Status_Valid;
                END;

                ALTER TABLE dbo.Transactions ADD CONSTRAINT CK_Transactions_Status_Valid CHECK (TransactionStatus IN (N'Pending', N'Reserved', N'Active', N'Maintenance', N'Completed', N'Cancelled'));
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Transactions', N'ReturnCondition') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD ReturnCondition nvarchar(50) NULL;
                END;

                IF COL_LENGTH(N'dbo.Transactions', N'ReturnNotes') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD ReturnNotes nvarchar(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.Transactions', N'AdditionalCharge') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD AdditionalCharge decimal(18,2) NOT NULL CONSTRAINT DF_Transactions_AdditionalCharge DEFAULT 0;
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.Transactions', N'AmountPaid') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD AmountPaid decimal(18,2) NOT NULL CONSTRAINT DF_Transactions_AmountPaid DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.Transactions', N'BalanceAmount') IS NULL
                BEGIN
                    ALTER TABLE dbo.Transactions ADD BalanceAmount decimal(18,2) NOT NULL CONSTRAINT DF_Transactions_BalanceAmount DEFAULT 0;
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.Transactions', N'AmountPaid') IS NOT NULL
               AND COL_LENGTH(N'dbo.Transactions', N'BalanceAmount') IS NOT NULL
            BEGIN
                UPDATE dbo.Transactions
                SET AmountPaid = CASE
                        WHEN PaymentStatus = N'Paid' THEN TotalAmount
                        ELSE ISNULL(AmountPaid, 0)
                    END,
                    BalanceAmount = TotalAmount - CASE
                        WHEN PaymentStatus = N'Paid' THEN TotalAmount
                        ELSE ISNULL(AmountPaid, 0)
                    END,
                    PaymentStatus = CASE
                        WHEN CASE WHEN PaymentStatus = N'Paid' THEN TotalAmount ELSE ISNULL(AmountPaid, 0) END <= 0 THEN N'Unpaid'
                        WHEN CASE WHEN PaymentStatus = N'Paid' THEN TotalAmount ELSE ISNULL(AmountPaid, 0) END < TotalAmount THEN N'Partial'
                        ELSE N'Paid'
                    END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'dbo.CK_Transactions_Status_Valid', N'C') IS NOT NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM sys.check_constraints
                        WHERE name = N'CK_Transactions_Status_Valid'
                          AND definition LIKE N'%Pending%'
                   )
                BEGIN
                    ALTER TABLE dbo.Transactions DROP CONSTRAINT CK_Transactions_Status_Valid;
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                IF OBJECT_ID(N'dbo.UQ_Transactions_TransactionCode', N'UQ') IS NULL
                   AND NOT EXISTS (
                        SELECT TransactionCode
                        FROM dbo.Transactions
                        GROUP BY TransactionCode
                        HAVING COUNT(1) > 1
                   )
                BEGIN
                    ALTER TABLE dbo.Transactions
                    ADD CONSTRAINT UQ_Transactions_TransactionCode UNIQUE (TransactionCode);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_DateRange_Valid', N'C') IS NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE StartDate > EndDate)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_DateRange_Valid CHECK (StartDate <= EndDate);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_DailyRate_Positive', N'C') IS NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE DailyRate <= 0)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_DailyRate_Positive CHECK (DailyRate > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_TotalDays_Positive', N'C') IS NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE TotalDays <= 0)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_TotalDays_Positive CHECK (TotalDays > 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_TotalAmount_NonNegative', N'C') IS NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE TotalAmount < 0)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_TotalAmount_NonNegative CHECK (TotalAmount >= 0);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_AmountPaid_Valid', N'C') IS NULL
                   AND COL_LENGTH(N'dbo.Transactions', N'AmountPaid') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE AmountPaid < 0 OR AmountPaid > TotalAmount)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_AmountPaid_Valid CHECK (AmountPaid >= 0 AND AmountPaid <= TotalAmount);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_BalanceAmount_Valid', N'C') IS NULL
                   AND COL_LENGTH(N'dbo.Transactions', N'BalanceAmount') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM dbo.Transactions WHERE BalanceAmount < 0 OR BalanceAmount <> TotalAmount - AmountPaid)
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_BalanceAmount_Valid CHECK (BalanceAmount >= 0 AND BalanceAmount = TotalAmount - AmountPaid);
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_ModeOfPayment_Valid', N'C') IS NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.Transactions
                        WHERE ModeOfPayment NOT IN (N'Cash', N'GCash', N'Bank Transfer', N'Other')
                   )
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_ModeOfPayment_Valid CHECK (ModeOfPayment IN (N'Cash', N'GCash', N'Bank Transfer', N'Other'));
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_PaymentStatus_Valid', N'C') IS NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.Transactions
                        WHERE PaymentStatus NOT IN (N'Unpaid', N'Partial', N'Paid')
                   )
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_PaymentStatus_Valid CHECK (PaymentStatus IN (N'Unpaid', N'Partial', N'Paid'));
                END;

                IF OBJECT_ID(N'dbo.CK_Transactions_Status_Valid', N'C') IS NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.Transactions
                        WHERE TransactionStatus NOT IN (N'Pending', N'Reserved', N'Active', N'Completed', N'Cancelled')
                   )
                BEGIN
                    ALTER TABLE dbo.Transactions WITH CHECK
                    ADD CONSTRAINT CK_Transactions_Status_Valid CHECK (TransactionStatus IN (N'Pending', N'Reserved', N'Active', N'Completed', N'Cancelled'));
                END;
            END;
            """, connection, transaction);

        // 8. Transaction Payments Ledger
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TransactionPayments
                (
                    TransactionPaymentId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TransactionId int NOT NULL,
                    PaymentDate datetime2 NOT NULL DEFAULT sysdatetime(),
                    Amount decimal(18,2) NOT NULL,
                    ModeOfPayment nvarchar(30) NOT NULL,
                    ReferenceNumber nvarchar(100) NULL,
                    ReceiptFilePath nvarchar(500) NULL,
                    Notes nvarchar(300) NULL,
                    CreatedByUserId int NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    IsArchived bit NOT NULL DEFAULT 0,
                    CONSTRAINT FK_TransactionPayments_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.Transactions(TransactionId),
                    CONSTRAINT FK_TransactionPayments_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT CK_TransactionPayments_Amount_Positive CHECK (Amount > 0),
                    CONSTRAINT CK_TransactionPayments_Mode_Valid CHECK (ModeOfPayment IN (N'Cash', N'GCash', N'Bank Transfer', N'Other'))
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.TransactionPayments', N'PaymentCategory') IS NULL
                BEGIN
                    ALTER TABLE dbo.TransactionPayments ADD PaymentCategory nvarchar(50) NULL;
                    
                    EXEC('UPDATE dbo.TransactionPayments SET PaymentCategory = N''Rental Payment'' WHERE PaymentCategory IS NULL');
                    
                    ALTER TABLE dbo.TransactionPayments ALTER COLUMN PaymentCategory nvarchar(50) NOT NULL;
                    ALTER TABLE dbo.TransactionPayments ADD CONSTRAINT DF_TransactionPayments_PaymentCategory DEFAULT N'Rental Payment' FOR PaymentCategory;
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
            BEGIN
                -- Migrate existing AmountPaid from Transactions to TransactionPayments if no non-archived payments exist yet.
                INSERT INTO dbo.TransactionPayments
                (
                    TransactionId,
                    PaymentDate,
                    Amount,
                    ModeOfPayment,
                    Notes,
                    CreatedByUserId,
                    CreatedAt
                )
                SELECT
                    transactions.TransactionId,
                    transactions.CreatedAt,
                    transactions.AmountPaid,
                    transactions.ModeOfPayment,
                    N'Migrated initial payment',
                    transactions.CreatedByUserId,
                    transactions.CreatedAt
                FROM dbo.Transactions AS transactions
                WHERE transactions.AmountPaid > 0
                  AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.TransactionPayments AS payments
                    WHERE payments.TransactionId = transactions.TransactionId
                      AND payments.IsArchived = 0
                  );
            END;
            """, connection, transaction);

        // 9. Customers Schema Updates
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

                IF COL_LENGTH(N'dbo.Customers', N'ValidIdFilePath') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD ValidIdFilePath nvarchar(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'SelfieWithValidIdFilePath') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD SelfieWithValidIdFilePath nvarchar(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.Customers', N'IsWalkIn') IS NULL
                BEGIN
                    ALTER TABLE dbo.Customers ADD IsWalkIn bit NOT NULL DEFAULT 0;
                END;
            END;
            """, connection, transaction);

        // 9. Customers Data Migration (Wrapped in sp_executesql to prevent parser errors)
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
            """, connection, transaction);

        // 10. Customers Constraints
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                -- Make FirstName and LastName nullable to support Company-only clients
                IF COL_LENGTH(N'dbo.Customers', N'FirstName') IS NOT NULL
                    ALTER TABLE dbo.Customers ALTER COLUMN FirstName nvarchar(100) NULL;
                
                IF COL_LENGTH(N'dbo.Customers', N'LastName') IS NOT NULL
                    ALTER TABLE dbo.Customers ALTER COLUMN LastName nvarchar(100) NULL;

                UPDATE dbo.Customers
                SET BlacklistReason = N'Legacy blacklist record'
                WHERE IsBlacklisted = 1
                  AND (BlacklistReason IS NULL OR LEN(LTRIM(RTRIM(BlacklistReason))) = 0);

                UPDATE dbo.Customers
                SET BlacklistReason = NULL
                WHERE IsBlacklisted = 0
                  AND BlacklistReason IS NOT NULL;

                IF OBJECT_ID(N'dbo.UQ_Customers_PhoneNumber', N'UQ') IS NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM dbo.Customers
                       GROUP BY PhoneNumber
                       HAVING COUNT(1) > 1
                   )
                BEGIN
                    ALTER TABLE dbo.Customers WITH CHECK
                    ADD CONSTRAINT UQ_Customers_PhoneNumber UNIQUE (PhoneNumber);
                END;

                -- Conditional Constraints for Names
                IF OBJECT_ID(N'dbo.CK_Customers_FirstName_NotEmpty', N'C') IS NOT NULL
                    ALTER TABLE dbo.Customers DROP CONSTRAINT CK_Customers_FirstName_NotEmpty;
                
                ALTER TABLE dbo.Customers WITH CHECK ADD CONSTRAINT CK_Customers_FirstName_NotEmpty 
                    CHECK (CustomerType <> N'Rental' OR (FirstName IS NOT NULL AND LEN(LTRIM(RTRIM(FirstName))) > 0));

                IF OBJECT_ID(N'dbo.CK_Customers_LastName_NotEmpty', N'C') IS NOT NULL
                    ALTER TABLE dbo.Customers DROP CONSTRAINT CK_Customers_LastName_NotEmpty;

                ALTER TABLE dbo.Customers WITH CHECK ADD CONSTRAINT CK_Customers_LastName_NotEmpty 
                    CHECK (CustomerType <> N'Rental' OR (LastName IS NOT NULL AND LEN(LTRIM(RTRIM(LastName))) > 0));

                IF OBJECT_ID(N'dbo.CK_Customers_CompanyName_NotEmpty', N'C') IS NOT NULL
                    ALTER TABLE dbo.Customers DROP CONSTRAINT CK_Customers_CompanyName_NotEmpty;

                ALTER TABLE dbo.Customers WITH CHECK ADD CONSTRAINT CK_Customers_CompanyName_NotEmpty 
                    CHECK (CustomerType <> N'Maintenance' OR (CompanyName IS NOT NULL AND LEN(LTRIM(RTRIM(CompanyName))) > 0));

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
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE IsWalkIn = 1)
                   AND EXISTS (SELECT 1 FROM dbo.Customers WHERE PhoneNumber = N'00000000000')
                BEGIN
                    UPDATE dbo.Customers
                    SET IsWalkIn = 1,
                        UpdatedAt = sysdatetime()
                    WHERE CustomerId =
                    (
                        SELECT TOP 1 CustomerId
                        FROM dbo.Customers
                        WHERE PhoneNumber = N'00000000000'
                        ORDER BY IsArchived, CustomerId
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE IsWalkIn = 1)
                BEGIN
                    INSERT INTO dbo.Customers
                    (
                        FirstName,
                        LastName,
                        PhoneNumber,
                        IsBlacklisted,
                        BlacklistReason,
                        IsWalkIn,
                        IsArchived
                    )
                    VALUES
                    (
                        N'Walk-In',
                        N'Customer',
                        N'00000000000',
                        0,
                        NULL,
                        1,
                        0
                    );
                END;
            END;
            """, connection, transaction);

        // 11. Indexes
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
            """, connection, transaction);

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
            """, connection, transaction);

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
            """, connection, transaction);

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
            """, connection, transaction);

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
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_CustomerId'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_CustomerId
                ON dbo.Transactions (CustomerId);
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_CarId'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_CarId
                ON dbo.Transactions (CarId);
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_FleetScheduleId'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_FleetScheduleId
                ON dbo.Transactions (FleetScheduleId);
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'UX_Transactions_FleetScheduleId_NotArchived'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
               AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.Transactions
                    WHERE IsArchived = 0
                    GROUP BY FleetScheduleId
                    HAVING COUNT(1) > 1
               )
            BEGIN
                CREATE UNIQUE NONCLUSTERED INDEX UX_Transactions_FleetScheduleId_NotArchived
                ON dbo.Transactions (FleetScheduleId)
                WHERE IsArchived = 0;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_DateRange'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_DateRange
                ON dbo.Transactions (StartDate, EndDate);
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_TransactionStatus'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_TransactionStatus
                ON dbo.Transactions (TransactionStatus);
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Transactions', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Transactions_IsArchived_CreatedAt'
                      AND object_id = OBJECT_ID(N'dbo.Transactions')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_IsArchived_CreatedAt
                ON dbo.Transactions (IsArchived, CreatedAt DESC);
            END;

            IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TransactionPayments_TransactionId'
                      AND object_id = OBJECT_ID(N'dbo.TransactionPayments')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_TransactionPayments_TransactionId
                ON dbo.TransactionPayments (TransactionId);
            END;

            IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TransactionPayments_PaymentDate'
                      AND object_id = OBJECT_ID(N'dbo.TransactionPayments')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_TransactionPayments_PaymentDate
                ON dbo.TransactionPayments (PaymentDate DESC);
            END;

            IF OBJECT_ID(N'dbo.TransactionPayments', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_TransactionPayments_IsArchived'
                      AND object_id = OBJECT_ID(N'dbo.TransactionPayments')
               )
            BEGIN
                CREATE NONCLUSTERED INDEX IX_TransactionPayments_IsArchived
                ON dbo.TransactionPayments (IsArchived);
            END;
            """, connection, transaction);

        // 12. Offsite Records
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.OffsiteRecords', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OffsiteRecords
                (
                    OffsiteRecordId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CarId int NOT NULL,
                    FleetScheduleId int NULL,
                    OffsiteType nvarchar(50) NOT NULL,
                    Status nvarchar(50) NOT NULL,
                    LocationName nvarchar(150) NULL,
                    ContactPerson nvarchar(100) NULL,
                    ContactNumber nvarchar(30) NULL,
                    StartDate date NOT NULL,
                    ExpectedReturnDate date NULL,
                    CompletedDate date NULL,
                    EstimatedCost decimal(18,2) NOT NULL DEFAULT 0,
                    ActualCost decimal(18,2) NOT NULL DEFAULT 0,
                    Notes nvarchar(500) NULL,
                    CreatedByUserId int NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedAt datetime2 NULL,
                    IsArchived bit NOT NULL DEFAULT 0,
                    WorkResult nvarchar(50) NULL,
                    ProofFilePath nvarchar(500) NULL,
                    AmountPaid decimal(18,2) NOT NULL DEFAULT 0,
                    BalanceAmount decimal(18,2) NOT NULL DEFAULT 0,
                    ModeOfPayment nvarchar(50) NOT NULL DEFAULT N'None / Not Applicable',
                    PaymentStatus nvarchar(50) NOT NULL DEFAULT N'Unpaid',
                    FollowUpRequired bit NOT NULL DEFAULT 0,
                    FollowUpReason nvarchar(300) NULL,
                    SuggestedNextAction nvarchar(300) NULL,
                    CompletedByUserId int NULL,
                    CONSTRAINT FK_OffsiteRecords_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars(CarId),
                    CONSTRAINT FK_OffsiteRecords_FleetSchedules FOREIGN KEY (FleetScheduleId) REFERENCES dbo.FleetSchedules(ScheduleId),
                    CONSTRAINT FK_OffsiteRecords_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId),
                    CONSTRAINT CK_OffsiteRecords_ExpectedReturnDate_Valid CHECK (ExpectedReturnDate IS NULL OR ExpectedReturnDate >= StartDate),
                    CONSTRAINT CK_OffsiteRecords_CompletedDate_Valid CHECK (CompletedDate IS NULL OR CompletedDate >= StartDate),
                    CONSTRAINT CK_OffsiteRecords_EstimatedCost_NonNegative CHECK (EstimatedCost >= 0),
                    CONSTRAINT CK_OffsiteRecords_ActualCost_NonNegative CHECK (ActualCost >= 0),
                    CONSTRAINT CK_OffsiteRecords_Type_Valid CHECK (OffsiteType IN (N'Maintenance', N'Repair', N'Cleaning', N'Inspection', N'Other')),
                    CONSTRAINT CK_OffsiteRecords_Status_Valid CHECK (Status IN (N'Pending', N'Reserved', N'Ongoing', N'Completed', N'Cancelled', N'Maintenance'))
                );
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.OffsiteRecords', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.OffsiteRecords', N'ProofFilePath') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD ProofFilePath nvarchar(500) NULL;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'AmountPaid') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD AmountPaid decimal(18,2) NOT NULL CONSTRAINT DF_OffsiteRecords_AmountPaid DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'BalanceAmount') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD BalanceAmount decimal(18,2) NOT NULL CONSTRAINT DF_OffsiteRecords_BalanceAmount DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'ModeOfPayment') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD ModeOfPayment nvarchar(50) NOT NULL CONSTRAINT DF_OffsiteRecords_ModeOfPayment DEFAULT N'None / Not Applicable';
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'PaymentStatus') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD PaymentStatus nvarchar(50) NOT NULL CONSTRAINT DF_OffsiteRecords_PaymentStatus DEFAULT N'Unpaid';
                END;

                -- Fix Status constraint
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_OffsiteRecords_Status_Valid')
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords DROP CONSTRAINT CK_OffsiteRecords_Status_Valid;
                END;
                
                ALTER TABLE dbo.OffsiteRecords ADD CONSTRAINT CK_OffsiteRecords_Status_Valid CHECK (Status IN (N'Pending', N'Reserved', N'Ongoing', N'Completed', N'Cancelled', N'Maintenance', N'Archived'));

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'FollowUpRequired') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD FollowUpRequired bit NOT NULL CONSTRAINT DF_OffsiteRecords_FollowUpRequired DEFAULT 0;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'FollowUpReason') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD FollowUpReason nvarchar(300) NULL;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'SuggestedNextAction') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD SuggestedNextAction nvarchar(300) NULL;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'CompletedByUserId') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD CompletedByUserId int NULL;
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'WorkResult') IS NULL
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords ADD WorkResult nvarchar(50) NULL;
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.OffsiteRecords', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.OffsiteRecords', N'WorkResult') IS NOT NULL
               AND COL_LENGTH(N'dbo.OffsiteRecords', N'FollowUpRequired') IS NOT NULL
            BEGIN
                UPDATE dbo.OffsiteRecords
                SET WorkResult = N'Completed'
                WHERE WorkResult IS NULL
                  AND Status = N'Completed';

                UPDATE dbo.OffsiteRecords
                SET FollowUpRequired = 0
                WHERE FollowUpRequired IS NULL;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.OffsiteRecords', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.OffsiteRecords', N'CompletedByUserId') IS NOT NULL
                   AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM sys.foreign_keys
                        WHERE name = N'FK_OffsiteRecords_CompletedByUsers'
                          AND parent_object_id = OBJECT_ID(N'dbo.OffsiteRecords')
                   )
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords WITH CHECK
                    ADD CONSTRAINT FK_OffsiteRecords_CompletedByUsers FOREIGN KEY (CompletedByUserId) REFERENCES dbo.Users(UserId);
                END;

                IF COL_LENGTH(N'dbo.OffsiteRecords', N'WorkResult') IS NOT NULL
                   AND NOT EXISTS (
                        SELECT 1
                        FROM sys.check_constraints
                        WHERE name = N'CK_OffsiteRecords_WorkResult_Valid'
                          AND parent_object_id = OBJECT_ID(N'dbo.OffsiteRecords')
                   )
                   AND NOT EXISTS (
                        SELECT 1
                        FROM dbo.OffsiteRecords
                        WHERE WorkResult IS NOT NULL
                          AND WorkResult NOT IN (N'Completed', N'Needs Follow-up', N'Not Repaired')
                   )
                BEGIN
                    ALTER TABLE dbo.OffsiteRecords WITH CHECK
                    ADD CONSTRAINT CK_OffsiteRecords_WorkResult_Valid CHECK (WorkResult IS NULL OR WorkResult IN (N'Completed', N'Needs Follow-up', N'Not Repaired'));
                END;
            END;
            """, connection, transaction);

        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.OffsiteRecords', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OffsiteRecords_CarId_Status'
                      AND object_id = OBJECT_ID(N'dbo.OffsiteRecords')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_OffsiteRecords_CarId_Status
                    ON dbo.OffsiteRecords (CarId, Status);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OffsiteRecords_Dates'
                      AND object_id = OBJECT_ID(N'dbo.OffsiteRecords')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_OffsiteRecords_Dates
                    ON dbo.OffsiteRecords (StartDate, ExpectedReturnDate);
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_OffsiteRecords_IsArchived'
                      AND object_id = OBJECT_ID(N'dbo.OffsiteRecords')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX IX_OffsiteRecords_IsArchived
                    ON dbo.OffsiteRecords (IsArchived);
                END;
            END;
            """, connection, transaction);

        // System Settings
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SystemSettings
                (
                    SettingKey nvarchar(100) NOT NULL PRIMARY KEY,
                    SettingValue nvarchar(max) NULL,
                    UpdatedAt datetime2 NOT NULL DEFAULT sysdatetime(),
                    UpdatedByUserId int NULL
                );

                INSERT INTO dbo.SystemSettings (SettingKey, SettingValue) VALUES
                (N'BusinessName', N'Nataraki Car Rental'),
                (N'ContactNumber', N''),
                (N'EmailAddress', N''),
                (N'BusinessAddress', N''),
                (N'BusinessRegionCode', N''),
                (N'BusinessRegionName', N''),
                (N'BusinessProvinceCode', N''),
                (N'BusinessProvinceName', N''),
                (N'BusinessCityCode', N''),
                (N'BusinessCityName', N''),
                (N'BusinessBarangayCode', N''),
                (N'BusinessBarangayName', N''),
                (N'BusinessStreetAddress', N''),
                (N'ThemeColor', N'#2563EB'),
                (N'SystemIconPath', N''),
                (N'SystemLogoMode', N'BuiltIn'),
                (N'SystemLogoIconKey', N'Car'),
                (N'LoginPosterPath', N''),
                (N'UseCustomLoginPoster', N'false'),
                (N'LoginDescription', N'Internal scheduling and record management system'),
                (N'ReportHeaderName', N'Nataraki Car Rental');
            END;
            """, connection, transaction);

        // 11. Notifications Table
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Notifications
                (
                    NotificationId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Title nvarchar(200) NOT NULL,
                    Message nvarchar(1000) NOT NULL,
                    Type nvarchar(50) NOT NULL DEFAULT 'Info',
                    IsRead bit NOT NULL DEFAULT 0,
                    RelatedEntityId int NULL,
                    RelatedModule nvarchar(100) NULL,
                    CreatedAt datetime2 NOT NULL DEFAULT sysdatetime()
                );

                CREATE NONCLUSTERED INDEX IX_Notifications_IsRead
                ON dbo.Notifications (IsRead, CreatedAt DESC);
            END;
            """, connection, transaction);
    }

    private static void SeedRoles(SqlConnection connection, SqlTransaction transaction)
    {
        InsertRoleIfMissing("Owner", "Full system owner access", connection, transaction, isSystemRole: true);
        NormalizeDuplicateOwnerRoles(connection, transaction);
        ArchiveUnusedPresetRoles(connection, transaction);
    }

    private static void NormalizeDuplicateOwnerRoles(SqlConnection connection, SqlTransaction transaction)
    {
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
            BEGIN
                DECLARE @CanonicalOwnerRoleId int;

                SELECT TOP 1 @CanonicalOwnerRoleId = r.RoleId
                FROM dbo.Roles r
                WHERE UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                ORDER BY
                    CASE WHEN EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId AND u.IsOwner = 1) THEN 0 ELSE 1 END,
                    r.IsArchived,
                    r.RoleId;

                IF @CanonicalOwnerRoleId IS NULL
                BEGIN
                    RETURN;
                END

                UPDATE dbo.Roles
                SET RoleName = N'Owner',
                    IsSystemRole = 1,
                    IsActive = 1,
                    IsArchived = 0,
                    UpdatedAt = sysdatetime()
                WHERE RoleId = @CanonicalOwnerRoleId;

                UPDATE u
                SET RoleId = @CanonicalOwnerRoleId,
                    UpdatedAt = sysdatetime()
                FROM dbo.Users u
                INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
                WHERE r.RoleId <> @CanonicalOwnerRoleId
                  AND (
                        UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                        OR UPPER(LTRIM(RTRIM(r.RoleName))) LIKE N'OWNER DUPLICATE%'
                      );

                UPDATE r
                SET IsArchived = 1,
                    IsActive = 0,
                    IsSystemRole = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                WHERE r.RoleId <> @CanonicalOwnerRoleId
                  AND (
                        UPPER(LTRIM(RTRIM(r.RoleName))) = N'OWNER'
                        OR UPPER(LTRIM(RTRIM(r.RoleName))) LIKE N'OWNER DUPLICATE%'
                      )
                  AND NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId);
            END;
            """, connection, transaction);
    }

    private static void ArchiveUnusedPresetRoles(SqlConnection connection, SqlTransaction transaction)
    {
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
            BEGIN
                UPDATE r
                SET IsArchived = 1,
                    IsActive = 0,
                    IsSystemRole = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                WHERE r.RoleName IN (N'Admin', N'Manager', N'Agent', N'Staff')
                  AND NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId);

                UPDATE r
                SET IsSystemRole = 0,
                    UpdatedAt = sysdatetime()
                FROM dbo.Roles r
                WHERE r.RoleName IN (N'Admin', N'Manager', N'Agent', N'Staff')
                  AND EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RoleId = r.RoleId);
            END;
            """, connection, transaction);
    }

    private static void InsertRoleIfMissing(string roleName, string description, SqlConnection connection, SqlTransaction transaction, bool isSystemRole = false)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE UPPER(LTRIM(RTRIM(RoleName))) = UPPER(LTRIM(RTRIM(@RoleName))))
            BEGIN
                INSERT INTO dbo.Roles (RoleName, Description, IsSystemRole)
                VALUES (@RoleName, @Description, @IsSystemRole);
            END;
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@RoleName", roleName);
        command.Parameters.AddWithValue("@Description", description);
        command.Parameters.AddWithValue("@IsSystemRole", isSystemRole);
        command.ExecuteNonQuery();
    }

    private static void SeedPermissions(SqlConnection connection, SqlTransaction transaction)
    {
        // Overview
        InsertPermissionIfMissing("Overview.View", "View Dashboard", "Overview", connection, transaction);

        // Fleet Schedule
        InsertPermissionIfMissing("FleetSchedule.View", "View Fleet Schedule", "Fleet Schedule", connection, transaction);
        InsertPermissionIfMissing("FleetSchedule.Create", "Create Schedule", "Fleet Schedule", connection, transaction);
        InsertPermissionIfMissing("FleetSchedule.Edit", "Edit Schedule", "Fleet Schedule", connection, transaction);
        InsertPermissionIfMissing("FleetSchedule.Cancel", "Cancel Schedule", "Fleet Schedule", connection, transaction);

        // Transactions
        InsertPermissionIfMissing("Transactions.View", "View Transactions", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.Create", "Create Transaction", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.Edit", "Edit Transaction", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.StartRental", "Start Rental", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.AddPayment", "Add Payment", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.Complete", "Complete Transaction", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.Cancel", "Cancel Transaction", "Transactions", connection, transaction);
        InsertPermissionIfMissing("Transactions.ArchiveRestore", "Archive/Restore Transactions", "Transactions", connection, transaction);

        // Customers
        InsertPermissionIfMissing("Customers.View", "View Customers", "Customers", connection, transaction);
        InsertPermissionIfMissing("Customers.Create", "Add Customer", "Customers", connection, transaction);
        InsertPermissionIfMissing("Customers.Edit", "Edit Customer", "Customers", connection, transaction);
        InsertPermissionIfMissing("Customers.Blacklist", "Manage Blacklist", "Customers", connection, transaction);
        InsertPermissionIfMissing("Customers.ArchiveRestore", "Archive/Restore Customers", "Customers", connection, transaction);

        // Car Garage
        InsertPermissionIfMissing("Cars.View", "View Car Garage", "Car Garage", connection, transaction);
        InsertPermissionIfMissing("Cars.Create", "Add Car", "Car Garage", connection, transaction);
        InsertPermissionIfMissing("Cars.Edit", "Edit Car", "Car Garage", connection, transaction);
        InsertPermissionIfMissing("Cars.ArchiveRestore", "Archive/Restore Cars", "Car Garage", connection, transaction);

        // Offsite
        InsertPermissionIfMissing("Offsite.View", "View Offsite Records", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.Create", "Create Offsite Record", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.Edit", "Edit Offsite Record", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.Complete", "Complete Offsite Record", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.Cancel", "Cancel Offsite Record", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.ArchiveRestore", "Archive/Restore Offsite", "Offsite", connection, transaction);
        InsertPermissionIfMissing("Offsite.MapTracking", "Access Map Tracking", "Offsite", connection, transaction);

        // Activity Log
        InsertPermissionIfMissing("ActivityLog.View", "View Activity Logs", "Activity Log", connection, transaction);

        // Reports
        InsertPermissionIfMissing("Reports.View", "View Reports", "Reports", connection, transaction);
        InsertPermissionIfMissing("Reports.Export", "Export Reports", "Reports", connection, transaction);

        // Manage System
        InsertPermissionIfMissing("ManageSystem.View", "View Manage System", "Manage System", connection, transaction);
        InsertPermissionIfMissing("ManageSystem.Settings", "Edit System Settings", "Manage System", connection, transaction);
        InsertPermissionIfMissing("ManageSystem.Branding", "Edit Branding & Theme", "Manage System", connection, transaction);
        InsertPermissionIfMissing("ManageSystem.Users", "Manage Users", "Manage System", connection, transaction);
        InsertPermissionIfMissing("ManageSystem.Roles", "Manage Roles & Permissions", "Manage System", connection, transaction);
    }

    private static void InsertPermissionIfMissing(string key, string name, string module, SqlConnection connection, SqlTransaction transaction)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = @Key)
            BEGIN
                INSERT INTO dbo.Permissions (PermissionKey, PermissionName, ModuleName)
                VALUES (@Key, @Name, @Module);
            END;
            """;

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@Key", key);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Module", module);
        command.ExecuteNonQuery();
    }

    private static void SeedRolePermissions(SqlConnection connection, SqlTransaction transaction)
    {
        // 1. Owner - All Permissions
        ExecuteDatabaseCommand("""
            INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
            SELECT r.RoleId, p.PermissionId
            FROM dbo.Roles r CROSS JOIN dbo.Permissions p
            WHERE r.RoleName = N'Owner'
              AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
            """, connection, transaction);

    }

    private static void SeedDefaultDemoOwner(SqlConnection connection, SqlTransaction transaction)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE IsOwner = 1)
            BEGIN
                INSERT INTO dbo.Users
                (
                    RoleId,
                    Username,
                    PasswordHash,
                    FirstName,
                    LastName,
                    IsActive,
                    IsOwner,
                    IsArchived,
                    CreatedAt
                )
                SELECT
                    RoleId,
                    @Username,
                    @PasswordHash,
                    @FirstName,
                    @LastName,
                    1,
                    1,
                    0,
                    sysdatetime()
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

        using SqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("@Username", bootstrapUsername);
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);
        command.Parameters.AddWithValue("@FirstName", "System");
        command.Parameters.AddWithValue("@LastName", "Owner");
        command.ExecuteNonQuery();
    }

    private static void NormalizeOwnerUserFlags(SqlConnection connection, SqlTransaction transaction)
    {
        ExecuteDatabaseCommand("""
            IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE IsOwner = 1 AND IsActive = 1 AND IsArchived = 0)
                BEGIN
                    UPDATE u
                    SET IsOwner = 1,
                        IsActive = 1,
                        IsArchived = 0,
                        UpdatedAt = sysdatetime()
                    FROM dbo.Users u
                    INNER JOIN dbo.Roles r ON r.RoleId = u.RoleId
                    WHERE u.UserId =
                    (
                        SELECT TOP 1 u2.UserId
                        FROM dbo.Users u2
                        INNER JOIN dbo.Roles r2 ON r2.RoleId = u2.RoleId
                        WHERE UPPER(LTRIM(RTRIM(r2.RoleName))) = N'OWNER'
                          AND u2.IsArchived = 0
                        ORDER BY
                            u2.UserId
                    );
                END;
            END;
            """, connection, transaction);
    }

    private static void RepairInvalidOwnerPasswordHash(SqlConnection connection, SqlTransaction transaction)
    {
        string defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultDemoBootstrapOwnerPassword);
        const string selectSql = """
            SELECT TOP 1 UserId, PasswordHash
            FROM dbo.Users
            WHERE IsOwner = 1 AND IsActive = 1 AND IsArchived = 0
            ORDER BY UserId;
            """;
        const string updateSql = """
            UPDATE dbo.Users
            SET PasswordHash = @PasswordHash,
                UpdatedAt = sysdatetime()
            WHERE UserId = @UserId
              AND IsOwner = 1;
            """;

        using SqlCommand selectCommand = new(selectSql, connection, transaction);
        using SqlDataReader reader = selectCommand.ExecuteReader();
        if (!reader.Read()) return;

        int userId = reader.GetInt32(0);
        string passwordHash = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        reader.Close();

        if (IsValidBCryptHash(passwordHash)) return;

        using SqlCommand updateCommand = new(updateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("@UserId", userId);
        updateCommand.Parameters.AddWithValue("@PasswordHash", defaultPasswordHash);
        updateCommand.ExecuteNonQuery();
    }

    private static bool IsValidBCryptHash(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) return false;
        return passwordHash.StartsWith("$2a$", StringComparison.Ordinal)
               || passwordHash.StartsWith("$2b$", StringComparison.Ordinal)
               || passwordHash.StartsWith("$2y$", StringComparison.Ordinal);
    }

    private static void ExecuteDatabaseCommand(string sql, SqlConnection connection, SqlTransaction transaction)
    {
        using SqlCommand command = new(sql, connection, transaction);
        command.ExecuteNonQuery();
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
