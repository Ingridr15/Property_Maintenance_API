using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Property_Maintenance_API.Api.Infrastructure;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Features;

public sealed record CreateRequestBody(
    int? UnitId, int? TenantId, RequestCategory? Category, RequestPriority? Priority, string? Description);

public sealed record ChangeStatusBody(RequestStatus? Status, int? TechnicianId);

public sealed record CreateVisitBody(int? TechnicianId, DateTimeOffset? VisitedAt, decimal? Cost, string? Notes);

public static class MaintenanceRequestCommands
{
    public static void MapMaintenanceRequestCommands(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/maintenance-requests").WithTags("Solicitudes");

        group.MapPost("/", Create)
            .WithSummary("Crea una solicitud. Reglas 1 y 2: solo el inquilino con contrato vigente en la unidad.");

        group.MapPatch("/{id:int}/status", ChangeStatus)
            .WithSummary("Cambia el estado. Reglas 3, 4 y 5. Para Asignada se envía technicianId.");

        group.MapPost("/{id:int}/visits", AddVisit)
            .WithSummary("Registra una visita. Regla 6: el costo no puede ser negativo.");
    }

    // ------------------------------------------------------------ POST /api/maintenance-requests
    private static async Task<IResult> Create(
        CreateRequestBody body, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        if (body.UnitId is null or <= 0)
            return Error.Validation("UNIT_REQUIRED", "unitId es obligatorio y debe ser positivo.", "unitId").ToProblem();
        if (body.TenantId is null or <= 0)
            return Error.Validation("TENANT_REQUIRED", "tenantId es obligatorio y debe ser positivo.", "tenantId").ToProblem();
        if (body.Category is null)
            return Error.Validation("CATEGORY_REQUIRED", "category es obligatorio: Plomeria, Electricidad o General.", "category").ToProblem();
        if (body.Priority is null)
            return Error.Validation("PRIORITY_REQUIRED", "priority es obligatorio: Baja, Media, Alta o Urgente.", "priority").ToProblem();
        if (string.IsNullOrWhiteSpace(body.Description) || body.Description.Length > 2000)
            return Error.Validation("DESCRIPTION_REQUIRED", "description es obligatoria (máximo 2000 caracteres).", "description").ToProblem();

        var now = clock.GetUtcNow().UtcDateTime;

        await using var connection = await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        if (await connection.ExecuteScalarAsync<int?>("SELECT 1 FROM dbo.Units WHERE UnitId = @id", new { id = body.UnitId }, tx) is null)
            return Error.NotFound("UNIT_NOT_FOUND", $"No existe la unidad {body.UnitId}.").ToProblem();

        if (await connection.ExecuteScalarAsync<int?>("SELECT 1 FROM dbo.Tenants WHERE TenantId = @id", new { id = body.TenantId }, tx) is null)
            return Error.NotFound("TENANT_NOT_FOUND", $"No existe el inquilino {body.TenantId}.").ToProblem();

        // Reglas 1 y 2. Se leen TODOS los contratos vigentes de la unidad. 
        var activeLeases = (await connection.QueryAsync<(int LeaseId, int TenantId)>(
                """
                SELECT LeaseId, TenantId
                FROM dbo.Leases WITH (UPDLOCK, HOLDLOCK)
                WHERE UnitId = @unitId
                  AND StartDate <= CAST(@now AS date)
                  AND (EndDate IS NULL OR EndDate >= CAST(@now AS date))
                """,
                new { unitId = body.UnitId, now }, tx))
            .Select(l => new ActiveLease(l.LeaseId, l.TenantId))
            .ToList();

        if (MaintenanceRequestRules.EnsureTenantCanReport(body.TenantId.Value, activeLeases) is { } error)
            return error.ToProblem();

        var id = await connection.QuerySingleAsync<int>(
            """
            INSERT INTO dbo.MaintenanceRequests (UnitId, ReportedByTenantId, Category, Priority, Status, Description, CreatedAt)
            OUTPUT INSERTED.MaintenanceRequestId
            VALUES (@UnitId, @TenantId, @Category, @Priority, 'Abierta', @Description, @now)
            """,
            new
            {
                body.UnitId,
                body.TenantId,
                Category = body.Category.ToString(),  
                Priority = body.Priority.ToString(),
                Description = body.Description.Trim(),
                now,
            }, tx);

        await tx.CommitAsync(ct);

        return Results.Created($"/api/maintenance-requests/{id}",
            new { Id = id, Status = "Abierta", body.UnitId, body.TenantId, body.Category, body.Priority, CreatedAt = now });
    }

    // ------------------------------------------------------------ PATCH /{id}/status
    private static async Task<IResult> ChangeStatus(
        int id, ChangeStatusBody body, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        if (body.Status is null)
            return Error.Validation("STATUS_REQUIRED", "status es obligatorio.", "status").ToProblem();
        if (body.TechnicianId is not null && body.Status != RequestStatus.Asignada)
            return Error.Validation("TECHNICIAN_NOT_ALLOWED", "technicianId solo se envía al pasar a Asignada.", "technicianId").ToProblem();

        var target = body.Status.Value;
        var now = clock.GetUtcNow().UtcDateTime;

        await using var connection = await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var current = await connection.QuerySingleOrDefaultAsync<(string Status, int? TechnicianId)?>(
            "SELECT Status, AssignedTechnicianId FROM dbo.MaintenanceRequests WITH (UPDLOCK, ROWLOCK) WHERE MaintenanceRequestId = @id",
            new { id }, tx);

        if (current is null)
            return Error.NotFound("REQUEST_NOT_FOUND", $"No existe la solicitud {id}.").ToProblem();

        TechnicianInfo? technician = null;
        if (body.TechnicianId is not null)
        {
            var tech = await connection.QuerySingleOrDefaultAsync<(int TechnicianId, bool IsActive)?>(
                "SELECT TechnicianId, IsActive FROM dbo.Technicians WHERE TechnicianId = @techId",
                new { techId = body.TechnicianId }, tx);
            if (tech is null)
                return Error.NotFound("TECHNICIAN_NOT_FOUND", $"No existe el técnico {body.TechnicianId}.").ToProblem();
            technician = new TechnicianInfo(tech.Value.TechnicianId, tech.Value.IsActive);
        }

        var visitCount = target == RequestStatus.Cerrada
            ? await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Visits WHERE MaintenanceRequestId = @id", new { id }, tx)
            : 0;

        var currentStatus = Enum.Parse<RequestStatus>(current.Value.Status);
        if (MaintenanceRequestRules.ValidateStatusChange(currentStatus, target, technician, visitCount) is { } error)
            return error.ToProblem();

        await connection.ExecuteAsync(
            """
            UPDATE dbo.MaintenanceRequests
            SET Status               = @target,
                AssignedTechnicianId = CASE WHEN @target = 'Asignada' THEN @techId ELSE AssignedTechnicianId END,
                ClosedAt             = CASE WHEN @target = 'Cerrada'  THEN @now    ELSE ClosedAt END,   -- regla 5
                UpdatedAt            = @now
            WHERE MaintenanceRequestId = @id
            """,
            new { id, target = target.ToString(), techId = technician?.TechnicianId, now }, tx);

        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            Id = id,
            PreviousStatus = currentStatus.ToString(),
            Status = target.ToString(),
            TechnicianId = technician?.TechnicianId ?? current.Value.TechnicianId,
            ClosedAt = target == RequestStatus.Cerrada ? now : (DateTime?)null,
        });
    }

    // ------------------------------------------------------------ POST /{id}/visits
    private static async Task<IResult> AddVisit(
        int id, CreateVisitBody body, SqlConnectionFactory db, TimeProvider clock, CancellationToken ct)
    {
        if (body.Cost is null)
            return Error.Validation("COST_REQUIRED", "cost es obligatorio.", "cost").ToProblem();
        if (body.Notes is { Length: > 2000 })
            return Error.Validation("NOTES_TOO_LONG", "notes no puede exceder 2000 caracteres.", "notes").ToProblem();

        var now = clock.GetUtcNow().UtcDateTime;
        var visitedAt = body.VisitedAt?.UtcDateTime ?? now;   // si no se manda, la visita es "ahora"

        await using var connection = await db.OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var request = await connection.QuerySingleOrDefaultAsync<(string Status, int? TechnicianId, DateTime CreatedAt)?>(
            "SELECT Status, AssignedTechnicianId, CreatedAt FROM dbo.MaintenanceRequests WITH (UPDLOCK, ROWLOCK) WHERE MaintenanceRequestId = @id",
            new { id }, tx);

        if (request is null)
            return Error.NotFound("REQUEST_NOT_FOUND", $"No existe la solicitud {id}.").ToProblem();

        var status = Enum.Parse<RequestStatus>(request.Value.Status);

        // Si no se indica tecnico, la visita es del tecnico asignado.
        var technicianId = body.TechnicianId ?? request.Value.TechnicianId;
        if (technicianId is null)
            return Error.Conflict("REQUEST_NOT_ACCEPTING_VISITS",
                $"La solicitud está {status} y no tiene técnico asignado.").ToProblem();

        var tech = await connection.QuerySingleOrDefaultAsync<(int TechnicianId, bool IsActive)?>(
            "SELECT TechnicianId, IsActive FROM dbo.Technicians WHERE TechnicianId = @techId",
            new { techId = technicianId }, tx);
        if (tech is null)
            return Error.NotFound("TECHNICIAN_NOT_FOUND", $"No existe el técnico {technicianId}.").ToProblem();

        var technician = new TechnicianInfo(tech.Value.TechnicianId, tech.Value.IsActive);
        if (MaintenanceRequestRules.ValidateNewVisit(status, request.Value.CreatedAt, technician, body.Cost.Value, visitedAt, now) is { } error)
            return error.ToProblem();

        var visitId = await connection.QuerySingleAsync<int>(
            """
            INSERT INTO dbo.Visits (MaintenanceRequestId, TechnicianId, VisitedAt, Notes, Cost)
            OUTPUT INSERTED.VisitId
            VALUES (@id, @techId, @visitedAt, @notes, @cost)
            """,
            new { id, techId = technician.TechnicianId, visitedAt, notes = body.Notes?.Trim(), cost = body.Cost }, tx);

        await tx.CommitAsync(ct);

        return Results.Created($"/api/maintenance-requests/{id}",
            new { Id = visitId, MaintenanceRequestId = id, technician.TechnicianId, VisitedAt = visitedAt, body.Cost, body.Notes });
    }
}