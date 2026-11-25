using System.Text.Json.Serialization;
using Dapper;
using Property_Maintenance_API.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<MigrationRunner>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Property Maintenance API"));

// Estado de la api
app.MapGet("/health", async (SqlConnectionFactory db, CancellationToken ct) =>
{
    await using var connection = await db.OpenAsync(ct);
    var tables = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables");
    return Results.Ok(new { status = "ok", tables });
});

await app.Services.GetRequiredService<MigrationRunner>().RunAsync(app.Lifetime.ApplicationStopping);

app.Run();