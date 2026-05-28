using System.Data;
using Dapper;
using NatarakiCarRental.Data;
using NatarakiCarRental.Models;

namespace NatarakiCarRental.Repositories;

public sealed class OffsiteRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public OffsiteRepository() : this(new DbConnectionFactory())
    {
    }

    public OffsiteRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<OffsiteRecordListItem>> GetListAsync(
        string? search = null,
        string? status = null,
        string? type = null,
        bool includeArchived = false,
        int page = 1,
        int pageSize = 10)
    {
        const string sql = """
            SELECT
                r.OffsiteRecordId,
                r.CarId,
                c.CarName,
                c.PlateNumber,
                r.OffsiteType,
                r.Status,
                r.LocationName,
                r.ContactPerson,
                r.ContactNumber,
                r.StartDate,
                r.ExpectedReturnDate,
                r.CompletedDate,
                r.EstimatedCost,
                r.ActualCost,
                r.ProofFilePath,
                r.WorkResult,
                r.FollowUpRequired,
                r.FollowUpReason,
                r.SuggestedNextAction
            FROM dbo.OffsiteRecords r
            INNER JOIN dbo.Cars c ON r.CarId = c.CarId
            WHERE (@IncludeArchived = 1 OR r.IsArchived = 0)
              AND (@Status IS NULL OR r.Status = @Status)
              AND (@Type IS NULL OR r.OffsiteType = @Type)
              AND (@Search IS NULL OR (
                    c.CarName LIKE @SearchPattern OR
                    c.PlateNumber LIKE @SearchPattern OR
                    r.LocationName LIKE @SearchPattern OR
                    r.Notes LIKE @SearchPattern
                  ))
            ORDER BY r.StartDate DESC, r.OffsiteRecordId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var items = await connection.QueryAsync<OffsiteRecordListItem>(sql, new
        {
            IncludeArchived = includeArchived,
            Status = string.IsNullOrWhiteSpace(status) ? null : status,
            Type = string.IsNullOrWhiteSpace(type) ? null : type,
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            SearchPattern = $"%{search}%",
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        });

        return items.ToList();
    }

    public async Task<int> CountAsync(
        string? search = null,
        string? status = null,
        string? type = null,
        bool includeArchived = false)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.OffsiteRecords r
            INNER JOIN dbo.Cars c ON r.CarId = c.CarId
            WHERE (@IncludeArchived = 1 OR r.IsArchived = 0)
              AND (@Status IS NULL OR r.Status = @Status)
              AND (@Type IS NULL OR r.OffsiteType = @Type)
              AND (@Search IS NULL OR (
                    c.CarName LIKE @SearchPattern OR
                    c.PlateNumber LIKE @SearchPattern OR
                    r.LocationName LIKE @SearchPattern OR
                    r.Notes LIKE @SearchPattern
                  ));
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            IncludeArchived = includeArchived,
            Status = string.IsNullOrWhiteSpace(status) ? null : status,
            Type = string.IsNullOrWhiteSpace(type) ? null : type,
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            SearchPattern = $"%{search}%"
        });
    }

    public async Task<OffsiteRecord?> GetByIdAsync(int offsiteRecordId)
    {
        const string sql = "SELECT * FROM dbo.OffsiteRecords WHERE OffsiteRecordId = @Id;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OffsiteRecord>(sql, new { Id = offsiteRecordId });
    }

    public async Task<OffsiteRecord?> GetByFleetScheduleIdAsync(int fleetScheduleId)
    {
        const string sql = "SELECT TOP 1 * FROM dbo.OffsiteRecords WHERE FleetScheduleId = @ScheduleId AND IsArchived = 0 ORDER BY OffsiteRecordId DESC;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OffsiteRecord>(sql, new { ScheduleId = fleetScheduleId });
    }

    public async Task<OffsiteRecord?> GetActiveByCarIdAsync(int carId)
    {
        const string sql = """
            SELECT TOP 1 *
            FROM dbo.OffsiteRecords
            WHERE CarId = @CarId
              AND Status = N'Ongoing'
              AND IsArchived = 0;
            """;
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OffsiteRecord>(sql, new { CarId = carId });
    }

    public async Task<int> AddAsync(OffsiteRecord record, IDbTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO dbo.OffsiteRecords
            (
                CarId, FleetScheduleId, OffsiteType, Status, LocationName,
                ContactPerson, ContactNumber, StartDate, ExpectedReturnDate,
                EstimatedCost, ActualCost, Notes, CreatedByUserId, ProofFilePath
            )
            OUTPUT INSERTED.OffsiteRecordId
            VALUES
            (
                @CarId, @FleetScheduleId, @OffsiteType, @Status, @LocationName,
                @ContactPerson, @ContactNumber, @StartDate, @ExpectedReturnDate,
                @EstimatedCost, @ActualCost, @Notes, @CreatedByUserId, @ProofFilePath
            );
            """;

        var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<int>(sql, record, transaction);
        }
        finally
        {
            if (transaction == null) connection.Dispose();
        }
    }

    public async Task<int> UpdateAsync(OffsiteRecord record, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.OffsiteRecords
            SET
                OffsiteType = @OffsiteType,
                LocationName = @LocationName,
                ContactPerson = @ContactPerson,
                ContactNumber = @ContactNumber,
                StartDate = @StartDate,
                ExpectedReturnDate = @ExpectedReturnDate,
                EstimatedCost = @EstimatedCost,
                ActualCost = @ActualCost,
                Notes = @Notes,
                ProofFilePath = @ProofFilePath,
                UpdatedAt = sysdatetime()
            WHERE OffsiteRecordId = @OffsiteRecordId
              AND IsArchived = 0;
            """;

        var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(sql, record, transaction);
        }
        finally
        {
            if (transaction == null) connection.Dispose();
        }
    }

    public async Task<int> CompleteAsync(CompleteOffsiteRecordRequest request, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.OffsiteRecords
            SET
                Status = N'Completed',
                CompletedDate = @CompletedDate,
                ActualCost = @AmountPaid,
                ProofFilePath = @ProofFilePath,
                WorkResult = @WorkResult,
                FollowUpRequired = @FollowUpRequired,
                FollowUpReason = @FollowUpReason,
                SuggestedNextAction = @SuggestedNextAction,
                CompletedByUserId = @CompletedByUserId,
                UpdatedAt = sysdatetime()
            WHERE OffsiteRecordId = @OffsiteRecordId
              AND Status = N'Ongoing'
              AND IsArchived = 0;
            """;

        var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(sql, request, transaction);
        }
        finally
        {
            if (transaction == null) connection.Dispose();
        }
    }

    public async Task<int> CancelAsync(int offsiteRecordId, IDbTransaction? transaction = null)
    {
        const string sql = """
            UPDATE dbo.OffsiteRecords
            SET
                Status = N'Cancelled',
                UpdatedAt = sysdatetime()
            WHERE OffsiteRecordId = @Id
              AND Status = N'Ongoing'
              AND IsArchived = 0;
            """;

        var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(sql, new { Id = offsiteRecordId }, transaction);
        }
        finally
        {
            if (transaction == null) connection.Dispose();
        }
    }

    public async Task<int> ArchiveAsync(int offsiteRecordId)
    {
        const string sql = "UPDATE dbo.OffsiteRecords SET IsArchived = 1, UpdatedAt = sysdatetime() WHERE OffsiteRecordId = @Id;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { Id = offsiteRecordId });
    }

    public async Task<int> RestoreAsync(int offsiteRecordId)
    {
        const string sql = "UPDATE dbo.OffsiteRecords SET IsArchived = 0, UpdatedAt = sysdatetime() WHERE OffsiteRecordId = @Id;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { Id = offsiteRecordId });
    }

    public async Task<bool> HasActiveOffsiteForCarAsync(int carId, int? excludeId = null)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM dbo.OffsiteRecords
            WHERE CarId = @CarId
              AND Status = N'Ongoing'
              AND IsArchived = 0
              AND (@ExcludeId IS NULL OR OffsiteRecordId <> @ExcludeId);
            """;
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, new { CarId = carId, ExcludeId = excludeId }) > 0;
    }
}
