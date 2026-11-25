/*
  Script: V001 (Version) - Esquema inicial.
  Proyecto: Property Maintenance API
  Autor: Ingrid Reyes
  Fecha de creacion: 24-NOV-26
  Descripcion: Esquema general para modelado de datos

  HISTORIAL DE VERSIONES
  ------------------------------------------------------------------
   Version     |       Fecha      |     Autor      |     Cambio
  ------------------------------------------------------------------
   1.0 (V001)        24-NOV-26       Ingrid Reyes       Creacion
*/

/* ---------------------------------------
    Properties : Inmueble administrado
 ---------------------------------------*/
CREATE TABLE dbo.Properties
(  
    PropertyId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Properties PRIMARY KEY,
    Name        nvarchar(150) NOT NULL,
    Address     nvarchar(250) NOT NULL,
    City        nvarchar(100) NOT NULL,
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt  datetime2(0) NOT NULL CONSTRAINT DF_Properties_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt  datetime2(0) NULL,
    CONSTRAINT CK_Properties_Name_NotBlank    CHECK (LEN(TRIM(Name)) > 0),
    CONSTRAINT CK_Properties_Address_NotBlank CHECK (LEN(TRIM(Address)) > 0),
    CONSTRAINT CK_Properties_City_NotBlank    CHECK (LEN(TRIM(City)) > 0)
);

GO

/* ---------------------------------------
    Units : Departamento o local dentro de un edificio
 ---------------------------------------*/
CREATE TABLE dbo.Units
(  
    UnitId     int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Units PRIMARY KEY,
    PropertyId int NOT NULL CONSTRAINT FK_Units_Properties REFERENCES dbo.Properties (PropertyId),
    Number     nvarchar(20) NOT NULL,
    Floor      smallint     NOT NULL,
    UnitType   varchar(20) NOT NULL,
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt  datetime2(0) NOT NULL CONSTRAINT DF_Units_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt  datetime2(0) NULL,
    CONSTRAINT UQ_Units_Property_Number UNIQUE (PropertyId, Number), 
    CONSTRAINT CK_Units_UnitType CHECK (UnitType IN ('Departamento', 'Local'))
);

GO

/* ---------------------------------------
    Tenants : Persona que renta
 ---------------------------------------*/
CREATE TABLE dbo.Tenants
(  
    TenantId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
    FullName   nvarchar(150) NOT NULL,
    Email     nvarchar(254) NOT NULL CONSTRAINT UQ_Tenants_Email UNIQUE,
    Phone     varchar(30)   NOT NULL,
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt  datetime2(0) NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt  datetime2(0) NULL,
    CONSTRAINT CK_Tenants_Email_Format CHECK (Email LIKE '%_@_%._%')
);

GO

/* ---------------------------------------
    Leases : Relación inquilino ↔ unidad en el tiempo
 ---------------------------------------*/
CREATE TABLE dbo.Leases
(  
    LeaseId    int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Leases PRIMARY KEY,
    UnitId      int NOT NULL CONSTRAINT FK_Leases_Units   REFERENCES dbo.Units (UnitId),
    TenantId    int NOT NULL CONSTRAINT FK_Leases_Tenants REFERENCES dbo.Tenants (TenantId),
    StartDate   date          NOT NULL,
    EndDate     date          NULL,                 -- NULL = sin fecha de término
    MonthlyRent decimal(12,2) NOT NULL,
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt  datetime2(0) NOT NULL CONSTRAINT DF_Leases_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt  datetime2(0) NULL,
    CONSTRAINT CK_Leases_Dates CHECK (EndDate IS NULL OR EndDate >= StartDate),
    CONSTRAINT CK_Leases_MonthlyRent CHECK (MonthlyRent > 0)
);

GO

/* ---------------------------------------
    REGLA #1 DE NEGOCIO : Una unidad tiene como máximo un contrato vigente a la vez. Un contrato está vigente si la fecha actual está entre su inicio y su fin (o no tiene fin).
 ---------------------------------------*/

  CREATE TRIGGER dbo.TRG_Leases_Overlap
  ON dbo.Leases
  AFTER INSERT, UPDATE
  AS
  BEGIN 
    SET NOCOUNT ON;

    -- Valida que el contrato no se traslape en fechas con ningún otro de la misma unidad.
    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.Leases AS l
        ON l.UnitId = i.UnitId
        AND l.LeaseId <> i.LeaseId
        WHERE l.StartDate <= ISNULL (i.EndDate, '9999-12-31')
        AND i.StartDate <= ISNULL (l.EndDate, '9999-12-31')
    )
    BEGIN   
        THROW 50001, N'Las fechas del contrato se traslapan con otro contrato de la misma unidad.', 1; 
    END;
  END;

  GO

  /* ---------------------------------------
    Technicians : Persona que atiende las solicitudes
 ---------------------------------------*/
CREATE TABLE dbo.Technicians
(
    TechnicianId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Technicians PRIMARY KEY,
    FullName     nvarchar(150) NOT NULL,
    Specialty    varchar(20)   NOT NULL,
    IsActive     bit           NOT NULL CONSTRAINT DF_Technicians_IsActive DEFAULT (1),
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt    datetime2(0) NOT NULL CONSTRAINT DF_Technicians_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt    datetime2(0) NULL,
    CONSTRAINT CK_Technicians_Specialty         CHECK (Specialty IN ('Plomeria', 'Electricidad', 'General')),
    CONSTRAINT CK_Technicians_FullName_NotBlank CHECK (LEN(TRIM(FullName)) > 0)
);

GO

/* ---------------------------------------
    MaintenanceRequests : Problema reportado
    REGLA #3 DE NEGOCIO : (la valida la API)
    Flujo de estados 
      Abierta -> Asignada -> EnProgreso -> Cerrada
      Abierta / Asignada -> Cancelada
 ---------------------------------------*/
CREATE TABLE dbo.MaintenanceRequests
(
    MaintenanceRequestId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenanceRequests PRIMARY KEY,
    UnitId               int            NOT NULL CONSTRAINT FK_MaintenanceRequests_Units       REFERENCES dbo.Units (UnitId),
    ReportedByTenantId   int            NOT NULL CONSTRAINT FK_MaintenanceRequests_Tenants     REFERENCES dbo.Tenants (TenantId),
    AssignedTechnicianId int            NULL     CONSTRAINT FK_MaintenanceRequests_Technicians REFERENCES dbo.Technicians (TechnicianId),
    Category             varchar(20)    NOT NULL,
    Priority             varchar(10)    NOT NULL,
    Status               varchar(20)    NOT NULL CONSTRAINT DF_MaintenanceRequests_Status DEFAULT ('Abierta'),
    Description          nvarchar(2000) NOT NULL,
    CreatedAt           datetime2(0)   NOT NULL CONSTRAINT DF_MaintenanceRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ClosedAt             datetime2(0)   NULL,
    UpdatedAt            datetime2(0)   NULL,
    CONSTRAINT CK_MaintenanceRequests_Category CHECK (Category IN ('Plomeria', 'Electricidad', 'General')),
    CONSTRAINT CK_MaintenanceRequests_Priority CHECK (Priority IN ('Baja', 'Media', 'Alta', 'Urgente')),
    CONSTRAINT CK_MaintenanceRequests_Status   CHECK (Status IN ('Abierta', 'Asignada', 'EnProgreso', 'Cerrada', 'Cancelada')),
    CONSTRAINT CK_MaintenanceRequests_Description_NotBlank CHECK (LEN(TRIM(Description)) > 0),
   
    -- REGLA #5 DE NEGOCIO: al cerrar se guarda la fecha de cierre (este CHECK).
    -- Que tenga al menos una visita lo valida la API (está en otra tabla).    
    CONSTRAINT CK_MaintenanceRequests_ClosedAt CHECK
    (
        (Status = 'Cerrada'  AND ClosedAt IS NOT NULL AND ClosedAt >= CreatedAt)
     OR (Status <> 'Cerrada' AND ClosedAt IS NULL)
    ),
    -- REGLA #4 DE NEGOCIO: desde Asignada siempre hay técnico (este CHECK).
    -- Que el técnico esté activo lo valida la API (está en otra tabla).    
    CONSTRAINT CK_MaintenanceRequests_Technician CHECK
    (
        (Status = 'Abierta' AND AssignedTechnicianId IS NULL)
     OR (Status IN ('Asignada', 'EnProgreso', 'Cerrada') AND AssignedTechnicianId IS NOT NULL)
     OR (Status = 'Cancelada')
    )
);

GO

/* ---------------------------------------
    Visits : Cada vez que un tecnico atiende una solicitud
 ---------------------------------------*/
CREATE TABLE dbo.Visits
(
    VisitId            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Visits PRIMARY KEY,
    MaintenanceRequestId int            NOT NULL CONSTRAINT FK_Visits_MaintenanceRequests REFERENCES dbo.MaintenanceRequests (MaintenanceRequestId),
    TechnicianId         int            NOT NULL CONSTRAINT FK_Visits_Technicians        REFERENCES dbo.Technicians (TechnicianId),
    VisitedAt            datetime2(0)   NOT NULL,
    Notes              nvarchar(2000) NULL,
    Cost                 decimal(12,2)  NOT NULL,
    -- Trazabilidad. CreatedAt se llena sola, UpdatedAt la asigna la API en cada update.
    CreatedAt           datetime2(0)   NOT NULL CONSTRAINT DF_Visits_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt            datetime2(0)   NULL,
    CONSTRAINT CK_Visits_Cost CHECK (Cost >= 0)    -- REGLA #6 DE NEGOCIO: Los costos de las visitas no pueden ser negativos.
);

GO