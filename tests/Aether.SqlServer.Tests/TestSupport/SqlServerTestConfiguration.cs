namespace Aether.SqlServer.Tests.TestSupport;

internal static class SqlServerTestConfiguration
{
    private const string ConnectionStringEnvironmentVariable =
        "AETHER_SQLSERVER_TEST_CONNECTION_STRING";

    public static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server integration tests require the '{ConnectionStringEnvironmentVariable}' environment variable.");
        }

        return connectionString;
    }
}