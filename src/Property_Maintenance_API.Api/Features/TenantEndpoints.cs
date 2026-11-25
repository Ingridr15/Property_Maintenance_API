using Dapper;
using Property_Maintenance_API.Api.Infrastructure;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Features;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tenants/{id:int}/history", GetHistory)
            .WithTags("Inquilinos")
            .WithSummary("Contratos del inquilino y sus solicitudes con el costo total de visitas");
    }

    private static async Task<IResult> GetHistory(int id, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        const string sql = """
            SELECT TenantId, FullName, Email, Phone FROM dbo.Tenants WHERE TenantId = @id;

            SELECT l.LeaseId, l.StartDate, l.EndDate, l.MonthlyRent,
                   CAST(CASE WHEN l.StartDate <= CAST(@today AS date)
                              AND (l.EndDate IS NULL OR l.EndDate >= CAST(@today AS date))
                             THEN 1 ELSE 0 END AS bit) AS IsActive,
                   u.UnitId, u.Number AS UnitNumber, p.PropertyId, p.Name AS PropertyName
            FROM dbo.Leases AS l
            JOIN dbo.Units AS u ON u.UnitId = l.UnitId
            JOIN dbo.Properties AS p ON p.PropertyId = u.PropertyId
            WHERE l.TenantId = @id
            ORDER BY l.StartDate DESC;

            SELECT r.MaintenanceRequestId AS Id, r.Category, r.Priority, r.Status, r.Description,
                   r.CreatedAt, r.ClosedAt, u.Number AS UnitNumber, p.Name AS PropertyName,
                   v.VisitCount, CAST(ISNULL(v.TotalCost, 0) AS decimal(14, 2)) AS TotalVisitCost
            FROM dbo.MaintenanceRequests AS r
            JOIN dbo.Units AS u ON u.UnitId = r.UnitId
            JOIN dbo.Properties AS p ON p.PropertyId = u.PropertyId
            CROSS APPLY (SELECT COUNT(*) AS VisitCount, SUM(vi.Cost) AS TotalCost
                         FROM dbo.Visits AS vi
                         WHERE vi.MaintenanceRequestId = r.MaintenanceRequestId) AS v
            WHERE r.ReportedByTenantId = @id
            ORDER BY r.CreatedAt DESC;
            """;

        var today = clock.GetUtcNow().UtcDateTime.Date;
        await using var connection = await db.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { id, today }, cancellationToken: ct));

        var tenant = await grid.ReadSingleOrDefaultAsync<TenantRow>();
        if (tenant is null)
            return Error.NotFound("TENANT_NOT_FOUND", $"No existe el inquilino {id}.").ToProblem();

        var leases = (await grid.ReadAsync<LeaseRow>()).ToList();
        var requests = (await grid.ReadAsync<RequestRow>()).ToList();

        return Results.Ok(new
        {
            tenant.TenantId,
            tenant.FullName,
            tenant.Email,
            tenant.Phone,
            Leases = leases,
            Requests = requests,
            TotalVisitCost = requests.Sum(r => r.TotalVisitCost),
        });
    }

    private sealed class TenantRow
    {
        public int TenantId { get; init; }
        public string FullName { get; init; } = "";
        public string Email { get; init; } = "";
        public string Phone { get; init; } = "";
    }

    public sealed class LeaseRow
    {
        public int LeaseId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public decimal MonthlyRent { get; init; }
        public bool IsActive { get; init; }
        public int UnitId { get; init; }
        public string UnitNumber { get; init; } = "";
        public int PropertyId { get; init; }
        public string PropertyName { get; init; } = "";
    }

    public sealed class RequestRow
    {
        public int Id { get; init; }
        public string Category { get; init; } = "";
        public string Priority { get; init; } = "";
        public string Status { get; init; } = "";
        public string Description { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public string UnitNumber { get; init; } = "";
        public string PropertyName { get; init; } = "";
        public int VisitCount { get; init; }
        public decimal TotalVisitCost { get; init; }
    }
}