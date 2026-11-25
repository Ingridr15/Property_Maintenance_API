using Dapper;
using Property_Maintenance_API.Api.Infrastructure;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Features;

public static class MaintenanceRequestQueries
{
    public static void MapMaintenanceRequestQueries(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/maintenance-requests", List)
            .WithTags("Solicitudes")
            .WithSummary("Listado paginado. Filtros: status, priority, propertyId, from/to (fecha de creación, yyyy-MM-dd, inclusive)");
    }

    private static async Task<IResult> List(
        RequestStatus? status, RequestPriority? priority, int? propertyId,
        DateOnly? from, DateOnly? to, SqlConnectionFactory db, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        if (page < 1)
            return Error.Validation("INVALID_PAGE", "page debe ser 1 o mayor.", field: "page").ToProblem();
        if (pageSize is < 1 or > 100)
            return Error.Validation("INVALID_PAGE_SIZE", "pageSize debe estar entre 1 y 100.", field: "pageSize").ToProblem();
        if (from > to)
            return Error.Validation("INVALID_RANGE", "'from' no puede ser posterior a 'to'.", field: "from").ToProblem();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        if (status is not null)
        {
            filters.Add("r.Status = @status");
            parameters.Add("status", status.ToString());
        }
        if (priority is not null)
        {
            filters.Add("r.Priority = @priority");
            parameters.Add("priority", priority.ToString());
        }
        if (propertyId is not null)
        {
            filters.Add("u.PropertyId = @propertyId");
            parameters.Add("propertyId", propertyId);
        }
        if (from is not null)
        {
            filters.Add("r.CreatedAt >= @from");
            parameters.Add("from", from.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (to is not null)
        {
            filters.Add("r.CreatedAt < @to");   // "to" inclusive: hasta antes del día siguiente
            parameters.Add("to", to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }

        parameters.Add("offset", (page - 1) * pageSize);
        parameters.Add("pageSize", pageSize);

        var where = filters.Count == 0 ? "" : "WHERE " + string.Join(" AND ", filters);

        var sql = $"""
            SELECT r.MaintenanceRequestId AS Id, r.Status, r.Priority, r.Category, r.CreatedAt, r.ClosedAt,
                   p.PropertyId, p.Name AS PropertyName,
                   u.UnitId, u.Number AS UnitNumber,
                   t.TenantId, t.FullName AS TenantName,
                   tech.TechnicianId, tech.FullName AS TechnicianName
            FROM dbo.MaintenanceRequests AS r
            JOIN dbo.Units AS u ON u.UnitId = r.UnitId
            JOIN dbo.Properties AS p ON p.PropertyId = u.PropertyId
            JOIN dbo.Tenants AS t ON t.TenantId = r.ReportedByTenantId
            LEFT JOIN dbo.Technicians AS tech ON tech.TechnicianId = r.AssignedTechnicianId
            {where}
            ORDER BY r.CreatedAt DESC, r.MaintenanceRequestId DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM dbo.MaintenanceRequests AS r
            JOIN dbo.Units AS u ON u.UnitId = r.UnitId
            {where};
            """;

        await using var connection = await db.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<RequestListRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return Results.Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
        });
    }

    public sealed class RequestListRow
    {
        public int Id { get; init; }
        public string Status { get; init; } = "";
        public string Priority { get; init; } = "";
        public string Category { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
        public int PropertyId { get; init; }
        public string PropertyName { get; init; } = "";
        public int UnitId { get; init; }
        public string UnitNumber { get; init; } = "";
        public int TenantId { get; init; }
        public string TenantName { get; init; } = "";
        public int? TechnicianId { get; init; }
        public string? TechnicianName { get; init; }
    }
}