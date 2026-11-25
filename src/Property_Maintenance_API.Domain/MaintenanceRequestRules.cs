namespace Property_Maintenance_API.Domain;

public sealed record ActiveLease(int LeaseId, int TenantId);
public sealed record TechnicianInfo(int TechnicianId, bool IsActive);

public static class MaintenanceRequestRules
{
    // REGLA #3: transiciones validas, todo lo que no este en esta seccion es invalido.
    private static readonly Dictionary<RequestStatus, RequestStatus[]> AllowedTransitions = new()
    {
        [RequestStatus.Abierta]    = [RequestStatus.Asignada, RequestStatus.Cancelada],
        [RequestStatus.Asignada]   = [RequestStatus.EnProgreso, RequestStatus.Cancelada],
        [RequestStatus.EnProgreso] = [RequestStatus.Cerrada],
        [RequestStatus.Cerrada]    = [],   
        [RequestStatus.Cancelada]  = [],   
    };

    public static bool IsTransitionAllowed(RequestStatus from, RequestStatus to) =>
        AllowedTransitions[from].Contains(to);

    // REGLAS #1 y #2: solo puede reportar el inquilino con el contrato vigente de la unidad.
    public static Error? EnsureTenantCanReport(int tenantId, IReadOnlyCollection<ActiveLease> activeLeases)
    {
        if (activeLeases.Count > 1)
            return Error.Conflict("UNIT_HAS_MULTIPLE_ACTIVE_LEASES",
                "La unidad tiene más de un contrato vigente.");

        if (activeLeases.Count == 0)
            return Error.Conflict("UNIT_WITHOUT_ACTIVE_LEASE",
                "La unidad no tiene un contrato vigente, nadie puede reportar solicitudes en ella.");

        if (activeLeases.First().TenantId != tenantId)
            return Error.Conflict("TENANT_WITHOUT_ACTIVE_LEASE_IN_UNIT",
                "El inquilino no tiene un contrato vigente en esta unidad.");

        return null;
    }

    // REGLAS #3, #4 y #5: cambio de estado.
    // technician: el tecnico enviado en la peticion.
    // visitCount: cuantas visitas tiene la solicitud.
    public static Error? ValidateStatusChange(
        RequestStatus current, RequestStatus target, TechnicianInfo? technician, int visitCount)
    {
        if (!IsTransitionAllowed(current, target))
        {
            var allowed = AllowedTransitions[current];
            var options = allowed.Length == 0 ? "ninguna (estado final)" : string.Join(", ", allowed);
            return Error.Conflict("INVALID_STATUS_TRANSITION",
                $"No se puede pasar de {current} a {target}. Desde {current} se permite: {options}.");
        }

        // REGLA #4
        if (target == RequestStatus.Asignada)
        {
            if (technician is null)
                return Error.Validation("TECHNICIAN_REQUIRED",
                    "Para asignar la solicitud se debe indicar technicianId.", field: "technicianId");

            if (!technician.IsActive)
                return Error.Conflict("TECHNICIAN_INACTIVE",
                    $"El tecnico {technician.TechnicianId} no esta activo.");
        }

        // REGLA #5 (la parte de las visitas, la fecha de cierre la pone la API)
        if (target == RequestStatus.Cerrada && visitCount == 0)
            return Error.Conflict("VISIT_REQUIRED_TO_CLOSE",
                "La solicitud no se puede cerrar porque no tiene visitas.");

        return null;
    }

    // REGLA #6, la visita debe tener costo >= 0, no estar en el futuro, ser posterior a la solicitud, y la solicitud debe estar en curso con un tecnico activo.
    public static Error? ValidateNewVisit(
        RequestStatus status, DateTime requestCreatedAt, TechnicianInfo technician,
        decimal cost, DateTime visitedAt, DateTime now)
    {
        if (cost < 0)
            return Error.Validation("NEGATIVE_COST", "El costo no puede ser negativo.", field: "cost");

        if (visitedAt > now.AddMinutes(5))   
            return Error.Validation("VISIT_IN_FUTURE", "La visita no puede estar en el futuro.", field: "visitedAt");

        if (visitedAt < requestCreatedAt)
            return Error.Validation("VISIT_BEFORE_REQUEST",
                "La visita no puede ser anterior a la solicitud.", field: "visitedAt");

        if (status is not (RequestStatus.Asignada or RequestStatus.EnProgreso))
            return Error.Conflict("REQUEST_NOT_ACCEPTING_VISITS",
                $"Solo se registran visitas en solicitudes Asignada o EnProgreso; esta está {status}.");

        if (!technician.IsActive)
            return Error.Conflict("TECHNICIAN_INACTIVE",
                $"El tecnico {technician.TechnicianId} no esta activo.");

        return null;
    }
}