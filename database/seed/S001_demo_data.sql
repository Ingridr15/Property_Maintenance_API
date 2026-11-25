/*
  Script: S001 - Datos de demostracion.
  Proyecto: Property Maintenance API
  Autor: Ingrid Reyes
  Fecha de creacion: 2026-11-25
  Descripcion:  3 edificios
                30 unidades repartidas entre los edificios
                20 inquilinos, con contratos vigentes y algunos ya terminados
                5 técnicos (al menos uno inactivo)
                100 solicitudes distribuidas en los últimos 6 meses, en todos los estados, con sus visitas.

*/

-- Si ya hay datos, no hace nada.
IF EXISTS (SELECT 1 FROM dbo.Properties)
BEGIN
    PRINT 'Seed omitido: ya hay datos.';
    RETURN;
END;

DECLARE @now   datetime2(0) = SYSUTCDATETIME();
DECLARE @today date         = CAST(@now AS date);


-- ---------- Edificios ----------
SET IDENTITY_INSERT dbo.Properties ON;
INSERT dbo.Properties (PropertyId, Name, Address, City) VALUES
 (1, N'Torre Alameda',          N'Av. Revolución 1250, Zona Centro',  N'Tijuana'),
 (2, N'Plaza Río',              N'Blvd. Agua Caliente 4558, Aviación', N'Tijuana'),
 (3, N'Residencial Costa Azul', N'Calle Primera 340, Centro',          N'Ensenada');
SET IDENTITY_INSERT dbo.Properties OFF;

-- ---------- Unidades ----------
SET IDENTITY_INSERT dbo.Units ON;
INSERT dbo.Units (UnitId, PropertyId, Number, Floor, UnitType) VALUES
 (1,1,N'101',1,'Departamento'),(2,1,N'102',1,'Departamento'),(3,1,N'103',1,'Departamento'),
 (4,1,N'201',2,'Departamento'),(5,1,N'202',2,'Departamento'),(6,1,N'203',2,'Departamento'),
 (7,1,N'301',3,'Departamento'),(8,1,N'302',3,'Departamento'),(9,1,N'303',3,'Departamento'),
 (10,1,N'401',4,'Departamento'),(11,1,N'402',4,'Departamento'),(12,1,N'403',4,'Departamento'),
 (13,2,N'L-01',0,'Local'),(14,2,N'L-02',0,'Local'),(15,2,N'L-03',0,'Local'),(16,2,N'L-04',0,'Local'),
 (17,2,N'101',1,'Departamento'),(18,2,N'102',1,'Departamento'),(19,2,N'103',1,'Departamento'),
 (20,2,N'201',2,'Departamento'),(21,2,N'202',2,'Departamento'),(22,2,N'203',2,'Departamento'),
 (23,3,N'101',1,'Departamento'),(24,3,N'102',1,'Departamento'),(25,3,N'103',1,'Departamento'),(26,3,N'104',1,'Departamento'),
 (27,3,N'201',2,'Departamento'),(28,3,N'202',2,'Departamento'),(29,3,N'203',2,'Departamento'),(30,3,N'204',2,'Departamento');
SET IDENTITY_INSERT dbo.Units OFF;

-- ---------- Inquilinos ----------
SET IDENTITY_INSERT dbo.Tenants ON;
INSERT dbo.Tenants (TenantId, FullName, Email, Phone) VALUES
 (1, N'Ana Sofía Martínez López',      N'ana.martinez@example.com',     '+52 664 555 1001'),
 (2, N'Carlos Eduardo Hernández Ruiz', N'carlos.hernandez@example.com', '+52 664 555 1002'),
 (3, N'María Fernanda García Soto',    N'maria.garcia@example.com',     '+52 664 555 1003'),
 (4, N'José Luis Rodríguez Peña',      N'jose.rodriguez@example.com',   '+52 664 555 1004'),
 (5, N'Daniela Torres Vega',           N'daniela.torres@example.com',   '+52 664 555 1005'),
 (6, N'Javier Morales Díaz',           N'javier.morales@example.com',   '+52 664 555 1006'),
 (7, N'Lucía Flores Castro',           N'lucia.flores@example.com',     '+52 664 555 1007'),
 (8, N'Ricardo Navarro Ortiz',         N'ricardo.navarro@example.com',  '+52 664 555 1008'),
 (9, N'Paola Jiménez Ramos',           N'paola.jimenez@example.com',    '+52 664 555 1009'),
 (10,N'Fernando Vargas Mendoza',       N'fernando.vargas@example.com',  '+52 664 555 1010'),
 (11,N'Gabriela Castillo Romero',      N'gabriela.castillo@example.com','+52 664 555 1011'),
 (12,N'Alejandro Ríos Aguilar',        N'alejandro.rios@example.com',   '+52 664 555 1012'),
 (13,N'Valentina Cruz Salazar',        N'valentina.cruz@example.com',   '+52 646 555 1013'),
 (14,N'Diego Reyes Medina',            N'diego.reyes@example.com',      '+52 646 555 1014'),
 (15,N'Camila Domínguez Luna',         N'camila.dominguez@example.com', '+52 646 555 1015'),
 (16,N'Sergio Gutiérrez Silva',        N'sergio.gutierrez@example.com', '+52 646 555 1016'),
 (17,N'Valeria Ortega Campos',         N'valeria.ortega@example.com',   '+52 664 555 1017'),
 (18,N'Emilio Sandoval Cortés',        N'emilio.sandoval@example.com',  '+52 664 555 1018'),
 (19,N'Regina Herrera Núñez',          N'regina.herrera@example.com',   '+52 646 555 1019'),
 (20,N'Andrés Estrada Ibarra',         N'andres.estrada@example.com',   '+52 646 555 1020');
SET IDENTITY_INSERT dbo.Tenants OFF;

-- ---------- Técnicos ----------
SET IDENTITY_INSERT dbo.Technicians ON;
INSERT dbo.Technicians (TechnicianId, FullName, Specialty, IsActive) VALUES
 (1, N'Jorge Ramírez Beltrán',        'Plomeria',     1),
 (2, N'Luis Fernando Ochoa Lara',     'Plomeria',     1),
 (3, N'Carmen Soto Villalobos',       'Electricidad', 1),
 (4, N'Miguel Ángel Torres Quintero', 'General',      1),
 (5, N'Ramón Ibarra Coronado',        'General',      0);   -- inactivo
SET IDENTITY_INSERT dbo.Technicians OFF;

-- ---------- Contratos ----------
-- 1-16 vigentes .
-- 17-23 terminados, el trigger valida que ninguno se traslape en la misma unidad.
SET IDENTITY_INSERT dbo.Leases ON;
INSERT dbo.Leases (LeaseId, UnitId, TenantId, StartDate, EndDate, MonthlyRent) VALUES
 (1,  1,  1, DATEADD(DAY,-420,@today), NULL,                      9500),
 (2,  2,  2, DATEADD(DAY,-450,@today), DATEADD(DAY,120,@today),   9500),
 (3,  3,  3, DATEADD(DAY,-480,@today), NULL,                      9750),
 (4,  4,  4, DATEADD(DAY,-510,@today), NULL,                     10000),
 (5,  5,  5, DATEADD(DAY,-540,@today), DATEADD(DAY,200,@today),  10000),
 (6,  6,  6, DATEADD(DAY,-570,@today), NULL,                     10250),
 (7,  8,  7, DATEADD(DAY,-600,@today), NULL,                     10500),
 (8,  13, 8, DATEADD(DAY,-630,@today), NULL,                     22000),
 (9,  14, 9, DATEADD(DAY,-660,@today), DATEADD(DAY,300,@today),  22000),
 (10, 16,10, DATEADD(DAY,-690,@today), NULL,                     24000),
 (11, 17,11, DATEADD(DAY,-720,@today), NULL,                      9000),
 (12, 19,12, DATEADD(DAY,-750,@today), DATEADD(DAY,150,@today),   9000),
 (13, 23,13, DATEADD(DAY,-780,@today), NULL,                      8500),
 (14, 24,14, DATEADD(DAY,-810,@today), NULL,                      8500),
 (15, 26,15, DATEADD(DAY,-840,@today), DATEADD(DAY,90,@today),    8750),
 (16, 28,16, DATEADD(DAY,-870,@today), NULL,                      8750),
 -- terminados
 (17, 7, 17, DATEADD(DAY,-700,@today),  DATEADD(DAY,-120,@today), 10500),
 (18, 15,18, DATEADD(DAY,-540,@today),  DATEADD(DAY,-40,@today),  23000),
 (19, 25,19, DATEADD(DAY,-480,@today),  DATEADD(DAY,-15,@today),   8500),
 (20, 30,20, DATEADD(DAY,-365,@today),  DATEADD(DAY,-150,@today),  8750),
 (21, 3, 17, DATEADD(DAY,-1300,@today), DATEADD(DAY,-760,@today),  9000),
 (22, 17,18, DATEADD(DAY,-1100,@today), DATEADD(DAY,-750,@today),  8500),
 (23, 24,19, DATEADD(DAY,-1200,@today), DATEADD(DAY,-900,@today),  8000);
SET IDENTITY_INSERT dbo.Leases OFF;

-- ---------- Solicitudes (100) ----------
WITH Numbers AS
(
    SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
),
Base AS
(
    SELECT n,
           -- 50 cerradas, 10 canceladas, 10 en progreso, 10 asignadas, 20 abiertas
           CASE WHEN n % 10 <= 4 THEN 'Cerrada'
                WHEN n % 10 = 5  THEN 'Cancelada'
                WHEN n % 10 = 6  THEN 'EnProgreso'
                WHEN n % 10 = 7  THEN 'Asignada'
                ELSE                  'Abierta' END                                  AS Status,
           CASE n % 3 WHEN 0 THEN 'Plomeria' WHEN 1 THEN 'Electricidad' ELSE 'General' END AS Category,
           CASE n % 4 WHEN 0 THEN 'Baja' WHEN 1 THEN 'Media' WHEN 2 THEN 'Alta' ELSE 'Urgente' END AS Priority,
           ((n - 1) % 16) + 1                                                        AS LeaseId   -- solo contratos vigentes
    FROM Numbers
)
SELECT b.n,
       b.Status,
       b.Category,
       b.Priority,
       l.UnitId,
       l.TenantId,
       CASE b.Status
            WHEN 'Cerrada'    THEN DATEADD(MINUTE, -((20 + (b.n * 37) % 160) * 1440 + (b.n * 53) % 600), @now)
            WHEN 'Cancelada'  THEN DATEADD(MINUTE, -((2  + (b.n * 13) % 170) * 1440 + (b.n * 53) % 600), @now)
            WHEN 'EnProgreso' THEN DATEADD(MINUTE, -((7  + (b.n * 7)  % 30)  * 1440 + (b.n * 53) % 600), @now)
            WHEN 'Asignada'   THEN DATEADD(MINUTE, -((1  + b.n % 15)         * 1440 + (b.n * 53) % 600), @now)
            ELSE                   DATEADD(MINUTE, -(120 + (b.n * 97) % 12000), @now)
       END AS CreatedAt,
       CASE b.Status WHEN 'Cerrada' THEN 1 + b.n % 3 WHEN 'EnProgreso' THEN 1 ELSE 0 END AS VisitCount,
       CASE WHEN b.Status = 'Abierta'                        THEN NULL
            WHEN b.Status = 'Cancelada' AND b.n % 2 = 0       THEN NULL
            WHEN b.Category = 'Plomeria'                      THEN 1 + b.n % 2
            WHEN b.Category = 'Electricidad'                  THEN 3
            WHEN b.Status = 'Cerrada' AND b.n % 4 = 0         THEN 5
            ELSE                                                   4
       END AS TechnicianId
INTO #Seed
FROM Base AS b
JOIN dbo.Leases AS l ON l.LeaseId = b.LeaseId;

SET IDENTITY_INSERT dbo.MaintenanceRequests ON;
INSERT dbo.MaintenanceRequests
    (MaintenanceRequestId, UnitId, ReportedByTenantId, AssignedTechnicianId,
     Category, Priority, Status, Description, CreatedAt, ClosedAt)
SELECT s.n, s.UnitId, s.TenantId, s.TechnicianId, s.Category, s.Priority, s.Status,
       CASE s.Category
            WHEN 'Plomeria'     THEN CASE s.n % 3 WHEN 0 THEN N'Fuga de agua debajo del lavabo.'
                                                  WHEN 1 THEN N'El inodoro no deja de correr agua.'
                                                  ELSE        N'Baja presión de agua en la regadera.' END
            WHEN 'Electricidad' THEN CASE s.n % 3 WHEN 0 THEN N'Se bota la pastilla al conectar el microondas.'
                                                  WHEN 1 THEN N'Contacto de la recámara sin corriente.'
                                                  ELSE        N'La lámpara del pasillo parpadea.' END
            ELSE                     CASE s.n % 3 WHEN 0 THEN N'La puerta principal no cierra bien.'
                                                  WHEN 1 THEN N'La ventana de la sala no corre.'
                                                  ELSE        N'Mosaico suelto en el piso de la cocina.' END
       END,
       s.CreatedAt,
       CASE WHEN s.Status = 'Cerrada' THEN DATEADD(HOUR, s.VisitCount * 24 + 30, s.CreatedAt) END
FROM #Seed AS s;
SET IDENTITY_INSERT dbo.MaintenanceRequests OFF;

-- ---------- Visitas ----------
INSERT dbo.Visits (MaintenanceRequestId, TechnicianId, VisitedAt, Notes, Cost)
SELECT s.n,
       s.TechnicianId,
       DATEADD(HOUR, k.k * 24 + s.n % 20, s.CreatedAt),
       CASE WHEN s.Status = 'Cerrada' AND k.k = s.VisitCount
            THEN N'Trabajo terminado y probado con el inquilino.'
            ELSE N'Revisión y diagnóstico de la falla.' END,
       -- Una de cada diez visitas sin costo.
       CASE WHEN (s.n + k.k) % 10 = 0 THEN 0 ELSE ((s.n * 7 + k.k * 13) % 40 + 2) * 50 END
FROM #Seed AS s
JOIN (VALUES (1), (2), (3)) AS k(k) ON k.k <= s.VisitCount;

DROP TABLE #Seed;