using Dapper;
using Property_Maintenance_API.Api.Infrastructure;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Features;

public sealed record CurrentTenant(
    int TenantId, string FullName, string Email, string Phone,
    int LeaseId, DateOnly StartDate, DateOnly? EndDate, decimal MonthlyRent);

public sealed record UnitWithTenant(int UnitId, string Number, int Floor, string UnitType, CurrentTenant? CurrentTenant);

public sealed record PropertyUnitsResponse(int PropertyId, string Name, string City, IReadOnlyList<UnitWithTenant> Units);

public static class PropertyEndpoints
{
    public static void MapPropertyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/properties/{id:int}/units", GetUnits)
            .WithTags("Edificios")
            .WithSummary("Unidades del edificio con su inquilino vigente");
    }

    private static async Task<IResult> GetUnits(int id, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);

        var property = await connection.QuerySingleOrDefaultAsync<PropertyRow>(new CommandDefinition(
            "SELECT PropertyId, Name, City FROM dbo.Properties WHERE PropertyId = @id",
            new { id }, cancellationToken: ct));

        if (property is null)
            return Error.NotFound("PROPERTY_NOT_FOUND", $"No existe el edificio {id}.").ToProblem();

        const string sql = """
            SELECT u.UnitId, u.Number, CAST(u.Floor AS int) AS Floor, u.UnitType,
                   l.LeaseId, l.StartDate, l.EndDate, l.MonthlyRent,
                   t.TenantId, t.FullName, t.Email, t.Phone
            FROM dbo.Units AS u
            LEFT JOIN dbo.Leases AS l
                   ON l.UnitId = u.UnitId
                  AND l.StartDate <= CAST(@today AS date)
                  AND (l.EndDate IS NULL OR l.EndDate >= CAST(@today AS date))
            LEFT JOIN dbo.Tenants AS t ON t.TenantId = l.TenantId
            WHERE u.PropertyId = @id
            ORDER BY u.Floor, u.Number
            """;

        var today = clock.GetUtcNow().UtcDateTime.Date;
        var rows = await connection.QueryAsync<UnitRow>(new CommandDefinition(sql, new { id, today }, cancellationToken: ct));

        var units = rows.Select(r => new UnitWithTenant(
            r.UnitId, r.Number, r.Floor, r.UnitType,
            r.LeaseId is null
                ? null
                : new CurrentTenant(r.TenantId!.Value, r.FullName!, r.Email!, r.Phone!, r.LeaseId.Value,
                    DateOnly.FromDateTime(r.StartDate!.Value),
                    r.EndDate is null ? null : DateOnly.FromDateTime(r.EndDate.Value),
                    r.MonthlyRent!.Value)))
            .ToList();

        return Results.Ok(new PropertyUnitsResponse(property.PropertyId, property.Name, property.City, units));
    }

    private sealed class PropertyRow
    {
        public int PropertyId { get; init; }
        public string Name { get; init; } = "";
        public string City { get; init; } = "";
    }

    private sealed class UnitRow
    {
        public int UnitId { get; init; }
        public string Number { get; init; } = "";
        public int Floor { get; init; }
        public string UnitType { get; init; } = "";
        public int? LeaseId { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public decimal? MonthlyRent { get; init; }
        public int? TenantId { get; init; }
        public string? FullName { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
    }
}