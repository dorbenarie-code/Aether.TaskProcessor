using Microsoft.Data.SqlClient;

namespace Aether.SqlServer.Tests.TestSupport;

internal sealed class SqlServerTestDatabase
{
    private readonly string _connectionString;

    public SqlServerTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public async Task EnsureJobsTableExistsAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME = 'Jobs';
            """;

        await using var command = new SqlCommand(sql, connection);

        var result = await command.ExecuteScalarAsync();
        var tableCount = Convert.ToInt32(result);

        if (tableCount == 0)
        {
            throw new InvalidOperationException(
                "The dbo.Jobs table does not exist. Run 001_create_jobs.sql before running SQL Server integration tests.");
        }
    }

    public async Task DeleteJobsByJobTypePrefixAsync(string jobTypePrefix)
    {
        if (string.IsNullOrWhiteSpace(jobTypePrefix))
        {
            throw new ArgumentException("Job type prefix is required.", nameof(jobTypePrefix));
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM dbo.Jobs
            WHERE JobType LIKE @JobTypePrefix;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@JobTypePrefix", $"{jobTypePrefix}%");

        await command.ExecuteNonQueryAsync();
    }
}