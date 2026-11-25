using System.Data;
using Dapper;
using Property_Maintenance_API.Api.Infrastructure;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Features;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reportes");

        group.MapGet("/properties-summary", GetPropertiesSummary)
            .WithSummary("Por edificio: abiertas, cerradas, horas promedio de resolución y costo (from/to yyyy-MM-dd, inclusive; por defecto últimos 30 días)");

        group.MapGet("/technicians-workload", GetTechniciansWorkload)
            .WithSummary("Por técnico activo: carga, visitas y costo del último mes, costo acumulado");
    }

    private static async Task<IResult> GetPropertiesSummary(
        DateOnly? from, DateOnly? to, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        var rangeTo = to ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var rangeFrom = from ?? rangeTo.AddDays(-29);

        if (rangeFrom > rangeTo)
            return Error.Validation("INVALID_RANGE", "'from' no puede ser posterior a 'to'.", field: "from").ToProblem();

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<PropertySummaryRow>(new CommandDefinition(
            "dbo.usp_Report_PropertiesSummary",
            new { From = rangeFrom.ToDateTime(TimeOnly.MinValue), To = rangeTo.AddDays(1).ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return Results.Ok(new { From = rangeFrom, To = rangeTo, Properties = rows });
    }

    private static async Task<IResult> GetTechniciansWorkload(SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddMonths(-1);

        await using var connection = await db.OpenAsync(ct);
        var rows = await connection.QueryAsync<TechnicianWorkloadRow>(new CommandDefinition(
            "dbo.usp_Report_TechniciansWorkload", new { Since = since },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return Results.Ok(new { Since = since, Technicians = rows });
    }

    public sealed class PropertySummaryRow
    {
        public int PropertyId { get; init; }
        public string PropertyName { get; init; } = "";
        public string City { get; init; } = "";
        public int CreatedRequests { get; init; }
        public int OpenRequests { get; init; }
        public int ClosedRequests { get; init; }
        public int CancelledRequests { get; init; }
        public decimal? AverageResolutionHours { get; init; }
        public int Visits { get; init; }
        public decimal TotalVisitCost { get; init; }
    }

    public sealed class TechnicianWorkloadRow
    {
        public int TechnicianId { get; init; }
        public string FullName { get; init; } = "";
        public string Specialty { get; init; } = "";
        public int AssignedRequests { get; init; }
        public int InProgressRequests { get; init; }
        public int ActiveRequests { get; init; }
        public int VisitsLastMonth { get; init; }
        public decimal CostLastMonth { get; init; }
        public decimal TotalCost { get; init; }
    }
}