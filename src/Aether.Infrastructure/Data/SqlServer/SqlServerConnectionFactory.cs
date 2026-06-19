using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Aether.Infrastructure.Data.SqlServer;

public sealed class SqlServerConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(IOptions<SqlServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
        {
            throw new InvalidOperationException("SQL Server connection string is missing.");
        }

        _connectionString = options.Value.ConnectionString;
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection();

        await connection.OpenAsync(cancellationToken);

        return connection;
    }
}