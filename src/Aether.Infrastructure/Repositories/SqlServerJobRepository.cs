using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Data.SqlServer;
using Microsoft.Data.SqlClient;

namespace Aether.Infrastructure.Repositories;

public sealed class SqlServerJobRepository : IJobRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerJobRepository(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Jobs (
                Id,
                JobType,
                Payload,
                Status,
                RetryCount,
                MaxRetries,
                ErrorMessage,
                CreatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                NextRetryAtUtc
            )
            VALUES (
                @Id,
                @JobType,
                @Payload,
                @Status,
                @RetryCount,
                @MaxRetries,
                @ErrorMessage,
                @CreatedAtUtc,
                @StartedAtUtc,
                @CompletedAtUtc,
                @NextRetryAtUtc
            );
            """;

        await using var command = new SqlCommand(sql, connection);

        AddJobParameters(command, job);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Job?> FindByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                Id,
                JobType,
                Payload,
                Status,
                RetryCount,
                MaxRetries,
                ErrorMessage,
                CreatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                NextRetryAtUtc
            FROM dbo.Jobs
            WHERE Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", jobId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadJob(reader);
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Jobs
            SET
                JobType = @JobType,
                Payload = @Payload,
                Status = @Status,
                RetryCount = @RetryCount,
                MaxRetries = @MaxRetries,
                ErrorMessage = @ErrorMessage,
                CreatedAtUtc = @CreatedAtUtc,
                StartedAtUtc = @StartedAtUtc,
                CompletedAtUtc = @CompletedAtUtc,
                NextRetryAtUtc = @NextRetryAtUtc
            WHERE Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);

        AddJobParameters(command, job);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Cannot update job '{job.Id}' because it does not exist.");
        }
    }

    public async Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                Id,
                JobType,
                Payload,
                Status,
                RetryCount,
                MaxRetries,
                ErrorMessage,
                CreatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                NextRetryAtUtc
            FROM dbo.Jobs
            ORDER BY CreatedAtUtc;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await ReadJobsAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Job>> GetFailedJobsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        const string sql = """
            SELECT
                Id,
                JobType,
                Payload,
                Status,
                RetryCount,
                MaxRetries,
                ErrorMessage,
                CreatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                NextRetryAtUtc
            FROM dbo.Jobs
            WHERE Status = @Status
            ORDER BY CreatedAtUtc;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Status", (int)JobStatus.Failed);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await ReadJobsAsync(reader, cancellationToken);
    }

    private static async Task<IReadOnlyCollection<Job>> ReadJobsAsync(
        SqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var jobs = new List<Job>();

        while (await reader.ReadAsync(cancellationToken))
        {
            jobs.Add(ReadJob(reader));
        }

        return jobs;
    }

    private static void AddJobParameters(SqlCommand command, Job job)
    {
        command.Parameters.AddWithValue("@Id", job.Id);
        command.Parameters.AddWithValue("@JobType", job.JobType);
        command.Parameters.AddWithValue("@Payload", job.Payload);
        command.Parameters.AddWithValue("@Status", (int)job.Status);
        command.Parameters.AddWithValue("@RetryCount", job.RetryCount);
        command.Parameters.AddWithValue("@MaxRetries", job.MaxRetries);
        command.Parameters.AddWithValue("@ErrorMessage", (object?)job.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAtUtc", job.CreatedAtUtc);
        command.Parameters.AddWithValue("@StartedAtUtc", (object?)job.StartedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@CompletedAtUtc", (object?)job.CompletedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@NextRetryAtUtc", (object?)job.NextRetryAtUtc ?? DBNull.Value);
    }

    private static Job ReadJob(SqlDataReader reader)
    {
        return Job.Restore(
            reader.GetGuid(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("JobType")),
            reader.GetString(reader.GetOrdinal("Payload")),
            (JobStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            reader.GetInt32(reader.GetOrdinal("RetryCount")),
            reader.GetInt32(reader.GetOrdinal("MaxRetries")),
            GetNullableString(reader, "ErrorMessage"),
            GetRequiredUtcDateTime(reader, "CreatedAtUtc"),
            GetNullableUtcDateTime(reader, "StartedAtUtc"),
            GetNullableUtcDateTime(reader, "CompletedAtUtc"),
            GetNullableUtcDateTime(reader, "NextRetryAtUtc"));
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static DateTime GetRequiredUtcDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        var value = reader.GetDateTime(ordinal);

        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? GetNullableUtcDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetDateTime(ordinal);

        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}