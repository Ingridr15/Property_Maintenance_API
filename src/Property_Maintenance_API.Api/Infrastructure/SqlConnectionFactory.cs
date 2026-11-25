using Microsoft.Data.SqlClient;

namespace Property_Maintenance_API.Api.Infrastructure;

// Abre conexiones a SQL Server.
public sealed class SqlConnectionFactory(IConfiguration configuration)
{
    public string ConnectionString =>
        configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en la configuración.");

    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}