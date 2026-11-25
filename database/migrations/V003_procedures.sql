/*
  Script: V003 (Version) - Procedures.
  Proyecto: Property Maintenance API
  Autor: Ingrid Reyes
  Fecha de creacion: 2026-11-24
  Descripcion: Procedures para los reportes de la API.

  HISTORIAL DE VERSIONES
  ------------------------------------------------------------------
   Version     |       Fecha      |     Autor      |     Cambio
  ------------------------------------------------------------------
   1.0 (V003)        2026-11-24      Ingrid Reyes       Creacion
*/

/* ---------------------------------------
    Resumen por edificio en un rango de fechas.
 ---------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_PropertiesSummary
    @From datetime2(0),  
    @To   datetime2(0)    
AS
BEGIN
    SET NOCOUNT ON;

    -- Las solicitudes que se crearon en el periodo, por edificio.
    WITH CreatedInRange AS
    (
        SELECT u.PropertyId,
               COUNT(*) AS CreatedRequests,
               SUM(CASE WHEN r.Status IN ('Abierta', 'Asignada', 'EnProgreso') THEN 1 ELSE 0 END) AS OpenRequests,                -- "Abiertas", las que siguen sin resolverse, esten o no asignadas
               SUM(CASE WHEN r.Status = 'Cancelada' THEN 1 ELSE 0 END) AS CancelledRequests
        FROM dbo.MaintenanceRequests AS r
        JOIN dbo.Units AS u ON u.UnitId = r.UnitId
        WHERE r.CreatedAt >= @From
          AND r.CreatedAt <  @To
        GROUP BY u.PropertyId
    ),

    -- Las que se cerraron en el periodo y cuánto tardaron.
    ClosedInRange AS
    (
        SELECT u.PropertyId,
               COUNT(*) AS ClosedRequests,
               -- Minutos entre que se reporto y se cerro, pasados a horas.
               AVG(CAST(DATEDIFF(MINUTE, r.CreatedAt, r.ClosedAt) AS decimal(18, 2)) / 60.0) AS AvgResolutionHours
        FROM dbo.MaintenanceRequests AS r
        JOIN dbo.Units AS u ON u.UnitId = r.UnitId
        WHERE r.ClosedAt IS NOT NULL    
          AND r.ClosedAt >= @From
          AND r.ClosedAt <  @To
        GROUP BY u.PropertyId
    ),

    -- Lo que se gasto en el periodo, segun la fecha de cada visita.
    VisitCostInRange AS
    (
        SELECT u.PropertyId,
               COUNT(*)    AS Visits,
               SUM(v.Cost) AS TotalVisitCost
        FROM dbo.Visits AS v
        -- Visita -> solicitud -> unidad -> edificio.
        JOIN dbo.MaintenanceRequests AS r ON r.MaintenanceRequestId = v.MaintenanceRequestId
        JOIN dbo.Units AS u ON u.UnitId = r.UnitId
        WHERE v.VisitedAt >= @From
          AND v.VisitedAt <  @To
        GROUP BY u.PropertyId
    )

    -- Se juntan los tres cálculos por edificio.
    SELECT p.PropertyId,
           p.Name                                             AS PropertyName,
           p.City,
           -- Si el edificio no tuvo actividad viene NULL; lo mostramos como 0.
           CAST(ISNULL(c.CreatedRequests, 0)   AS int)            AS CreatedRequests,
           CAST(ISNULL(c.OpenRequests, 0)      AS int)            AS OpenRequests,
           CAST(ISNULL(cl.ClosedRequests, 0)   AS int)            AS ClosedRequests,
           CAST(ISNULL(c.CancelledRequests, 0) AS int)            AS CancelledRequests,
           -- El promedio se deja en NULL si no hubo cerradas, "no aplica" no es lo mismo que "0 horas".
           CAST(cl.AvgResolutionHours          AS decimal(10, 2)) AS AverageResolutionHours,
           CAST(ISNULL(vc.Visits, 0)           AS int)            AS Visits,
           CAST(ISNULL(vc.TotalVisitCost, 0)   AS decimal(14, 2)) AS TotalVisitCost
    FROM dbo.Properties AS p
    LEFT JOIN CreatedInRange   AS c  ON c.PropertyId  = p.PropertyId
    LEFT JOIN ClosedInRange    AS cl ON cl.PropertyId = p.PropertyId
    LEFT JOIN VisitCostInRange AS vc ON vc.PropertyId = p.PropertyId
    ORDER BY p.Name;
END;
GO

/* ---------------------------------------
    Carga de trabajo de cada técnico activo.
 ---------------------------------------*/
CREATE OR ALTER PROCEDURE dbo.usp_Report_TechniciansWorkload
    @Since datetime2(0)  
AS
BEGIN
    SET NOCOUNT ON;

    -- Lo que cada tecnico tiene pendiente ahorita, asignadas o en progreso.
    WITH InCourseRequests AS
    (
        SELECT r.AssignedTechnicianId AS TechnicianId,
               SUM(CASE WHEN r.Status = 'Asignada'   THEN 1 ELSE 0 END) AS AssignedRequests,
               SUM(CASE WHEN r.Status = 'EnProgreso' THEN 1 ELSE 0 END) AS InProgressRequests
        FROM dbo.MaintenanceRequests AS r
        WHERE r.Status IN ('Asignada', 'EnProgreso')
        GROUP BY r.AssignedTechnicianId
    ),

    -- Lo que cada técnico ha hecho.
    VisitTotals AS
    (
        SELECT v.TechnicianId,
               SUM(CASE WHEN v.VisitedAt >= @Since THEN 1      ELSE 0 END) AS VisitsLastMonth,
               SUM(CASE WHEN v.VisitedAt >= @Since THEN v.Cost ELSE 0 END) AS CostLastMonth,
               SUM(v.Cost)                                                  AS TotalCost
        FROM dbo.Visits AS v
        GROUP BY v.TechnicianId
    )

    -- Se junta la info por tecnico. 
    SELECT t.TechnicianId,
           t.FullName,
           t.Specialty,
           CAST(ISNULL(a.AssignedRequests, 0)   AS int) AS AssignedRequests,
           CAST(ISNULL(a.InProgressRequests, 0) AS int) AS InProgressRequests,
           CAST(ISNULL(a.AssignedRequests, 0) + ISNULL(a.InProgressRequests, 0) AS int) AS ActiveRequests,
           CAST(ISNULL(vt.VisitsLastMonth, 0)   AS int)            AS VisitsLastMonth,
           CAST(ISNULL(vt.CostLastMonth, 0)     AS decimal(14, 2)) AS CostLastMonth,
           CAST(ISNULL(vt.TotalCost, 0)         AS decimal(14, 2)) AS TotalCost
    FROM dbo.Technicians AS t
    LEFT JOIN InCourseRequests AS a  ON a.TechnicianId  = t.TechnicianId
    LEFT JOIN VisitTotals      AS vt ON vt.TechnicianId = t.TechnicianId
    WHERE t.IsActive = 1  
    -- Primero el mas cargado, si empatan, el que mas visitas hizo, si siguen empatados, por nombre 
    ORDER BY ActiveRequests DESC, VisitsLastMonth DESC, t.FullName;
END;
GO