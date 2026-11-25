/*
  Script: V002 (Version) - Indices.
  Proyecto: Property Maintenance API
  Autor: Ingrid Reyes
  Fecha de creacion: 2026-11-25
  Descripcion: Indices para las consultas y reportes de la API. Cada indice
               indica la consulta a la que responde.

  HISTORIAL DE VERSIONES
  ------------------------------------------------------------------
   Version     |       Fecha      |     Autor      |     Cambio
  ------------------------------------------------------------------
   1.0 (V002)        2026-11-24      Ingrid Reyes       Creacion

*/

-- Contrato vigente de una unidad (crear solicitud, unidades de un edificio).
CREATE INDEX IX_Leases_UnitId_StartDate
    ON dbo.Leases (UnitId, StartDate)
    INCLUDE (EndDate, TenantId, MonthlyRent);
GO

-- Historial del inquilino 
CREATE INDEX IX_Leases_TenantId
    ON dbo.Leases (TenantId);
GO

-- Listado paginado: ordena por fecha de creacion (mas recientes primero) y
-- filtra por rango de fechas. Tambien lo usa el reporte por edificio.
CREATE INDEX IX_MaintenanceRequests_CreatedAt
    ON dbo.MaintenanceRequests (CreatedAt DESC)
    INCLUDE (Status, Priority, UnitId, ReportedByTenantId, AssignedTechnicianId);
GO

-- Listado filtrado por estado 
CREATE INDEX IX_MaintenanceRequests_Status_CreatedAt
    ON dbo.MaintenanceRequests (Status, CreatedAt DESC);
GO

-- Reporte de carga por tecnico: solicitudes Asignada/EnProgreso
CREATE INDEX IX_MaintenanceRequests_AssignedTechnicianId_Status
    ON dbo.MaintenanceRequests (AssignedTechnicianId, Status);
GO

-- Reporte por edificio: cerradas en el rango y tiempo de resolucion.
-- Filtrado: solo indexa las filas con ClosedAt (las cerradas)
CREATE INDEX IX_MaintenanceRequests_ClosedAt
    ON dbo.MaintenanceRequests (ClosedAt)
    INCLUDE (UnitId, CreatedAt)
    WHERE ClosedAt IS NOT NULL;
GO

-- Solicitudes de una unidad.
CREATE INDEX IX_MaintenanceRequests_UnitId
    ON dbo.MaintenanceRequests (UnitId);
GO

-- Solicitudes de un inquilino (historial).
CREATE INDEX IX_MaintenanceRequests_ReportedByTenantId
    ON dbo.MaintenanceRequests (ReportedByTenantId);
GO

-- Ccosto total de visitas por solicitud (historial, regla de cierre).
CREATE INDEX IX_Visits_MaintenanceRequestId
    ON dbo.Visits (MaintenanceRequestId)
    INCLUDE (Cost);
GO

-- Costo de las visitas realizadas en el rango.
CREATE INDEX IX_Visits_VisitedAt
    ON dbo.Visits (VisitedAt)
    INCLUDE (MaintenanceRequestId, Cost);
GO

-- Visitas del ultimo mes y costo por tecnico.
CREATE INDEX IX_Visits_TechnicianId_VisitedAt
    ON dbo.Visits (TechnicianId, VisitedAt)
    INCLUDE (Cost);
GO