namespace Aether.Infrastructure.Data.SqlServer;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";

    public string ConnectionString { get; init; } = string.Empty;
}