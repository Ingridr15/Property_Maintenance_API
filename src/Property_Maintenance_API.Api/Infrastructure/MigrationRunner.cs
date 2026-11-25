using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Property_Maintenance_API.Api.Infrastructure;

// Aplica los scripts que falten, en orden por nombre: primero database/migrations
// (estructura) y, si Database:SeedOnStartup es true, database/seed (datos de prueba).
// Lleva el registro en dbo.SchemaMigrations para no aplicar dos veces el mismo.
// Cada script corre en su propia transacción: o se aplica completo o no se aplica.
public sealed partial class MigrationRunner(
    SqlConnectionFactory factory, IConfiguration configuration, ILogger<MigrationRunner> logger)
{
    // Separa los lotes por las líneas que solo dicen GO (igual que sqlcmd).
    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    public async Task RunAsync(CancellationToken ct)
    {
        await WaitAndCreateDatabaseAsync(ct);

        await using var connection = await factory.OpenAsync(ct);

        await connection.ExecuteAsync("""
            IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
                CREATE TABLE dbo.SchemaMigrations
                (
                    ScriptName nvarchar(200) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
                    AppliedAt  datetime2(0)  NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAt DEFAULT (SYSUTCDATETIME())
                );
            """);

        var applied = (await connection.QueryAsync<string>("SELECT ScriptName FROM dbo.SchemaMigrations"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Primero la estructura; después, si está habilitado, los datos de prueba.
        var scripts = LoadScripts("migrations/").ToList();
        if (configuration.GetValue("Database:SeedOnStartup", false))
            scripts.AddRange(LoadScripts("seed/"));

        foreach (var (name, sql) in scripts)
        {
            if (applied.Contains(name))
                continue;

            logger.LogInformation("Aplicando migración {Script}", name);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

            foreach (var batch in GoSeparator().Split(sql).Where(b => !string.IsNullOrWhiteSpace(b)))
                await connection.ExecuteAsync(batch, transaction: transaction);

            await connection.ExecuteAsync(
                "INSERT INTO dbo.SchemaMigrations (ScriptName) VALUES (@name)", new { name }, transaction);

            await transaction.CommitAsync(ct);
        }

        logger.LogInformation("Base de datos al día");
    }

    // SQL Server en Docker tarda unos segundos en aceptar conexiones: reintenta.
    // Se conecta a "master" para crear la base si todavía no existe.
    private async Task WaitAndCreateDatabaseAsync(CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(factory.ConnectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var master = new SqlConnection(builder.ConnectionString);
                await master.OpenAsync(ct);
                await master.ExecuteAsync("""
                    IF DB_ID(@database) IS NULL
                    BEGIN
                        DECLARE @sql nvarchar(400) = N'CREATE DATABASE ' + QUOTENAME(@database);
                        EXEC (@sql);
                    END
                    """, new { database });
                return;
            }
            catch (SqlException ex) when (attempt < 30)
            {
                logger.LogWarning("SQL Server aun no responde (intento {Attempt}/30): {Error}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    // Lee los scripts embebidos de una carpeta ("migrations/" o "seed/"), ordenados por nombre.
    private static IEnumerable<(string Name, string Sql)> LoadScripts(string prefix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix))
            .Order()
            .ToList();

        // Sin migraciones no hay esquema: mejor fallar que arrancar a medias.
        if (prefix == "migrations/" && resources.Count == 0)
            throw new InvalidOperationException("No se encontraron scripts de migración embebidos. Revisa el .csproj.");

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            yield return (resource[prefix.Length..], reader.ReadToEnd());
        }
    }
}