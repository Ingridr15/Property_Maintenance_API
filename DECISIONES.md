# Decisiones técnicas

## Dapper + SQL escrito a mano (no EF Core)
La prueba evalúa el modelo de datos y el SQL. Con Dapper, el SQL que se revisa es exactamente el que se ejecuta. Los reportes son consultas con agregados, donde el SQL a mano es más claro y eficiente que LINQ. Costo asumido: más código de mapeo y sin change tracking.

## Scripts SQL versionados + runner propio
`database/migrations/V00N_*.sql` es SQL plano: se lee, se puede ejecutar con sqlcmd y es compatible con Azure SQL. La API los aplica al arrancar, en orden, cada uno en su propia transacción, y lleva el registro en `dbo.SchemaMigrations`. Se eligió un runner de ~80 líneas en lugar de DbUp para no agregar dependencias. Los datos de prueba van aparte (`database/seed`) porque no deben llegar a producción; se controlan con `Database:SeedOnStartup`.

## Reglas de negocio en dos capas
- **Base de datos**, para lo que no depende de la fecha ni de otras filas: CHECK de catálogos, costo >= 0, `ClosedAt` solo y siempre en Cerrada, técnico obligatorio desde Asignada, email único, y un trigger que impide contratos traslapados (regla 1).
- **API (proyecto Domain)**, para lo que depende de la fecha actual o de otras tablas: contrato vigente (regla 2), transiciones (regla 3), técnico activo (regla 4) y visitas para cerrar (regla 5). Son funciones puras, sin base de datos, fáciles de probar.

## Transacciones y concurrencia
Crear solicitud, cambiar estado y registrar visita leen y escriben en la misma transacción. La fila de la solicitud se lee con `UPDLOCK`, para que dos cambios simultáneos se ejecuten en serie (por ejemplo, que no se registre una visita mientras otra petición la cancela). Al crear, los contratos de la unidad se leen con `UPDLOCK, HOLDLOCK`, para que no cambien entre la validación y el INSERT.

## Índices
Cada índice de `V002` responde a una consulta concreta y está comentado. Se cubren las llaves foráneas usadas en JOINs, porque SQL Server no las indexa solo. Destaca un índice filtrado sobre `ClosedAt` (solo solicitudes cerradas) para el reporte por edificio.

## Reportes
Los dos son procedimientos almacenados. Cada métrica se agrega en su propio CTE y luego se une con LEFT JOIN, para evitar que unir solicitudes con visitas duplique conteos y para que aparezcan los edificios o técnicos sin actividad. Los rangos son semiabiertos `[from, to)`.

## Qué haría distinto con más tiempo
- Pruebas unitarias de las reglas del Domain y de integración de los endpoints (con Testcontainers).
- Pipeline de CI en GitHub Actions.
- Autenticación, y columnas `CreatedBy`/`UpdatedBy`. Las tablas tienen `CreatedAt`/`UpdatedAt`; el "quién" no se incluyó porque sin usuarios siempre tendría el mismo valor.- Bitácora de cambios de estado de las solicitudes.
- Concurrencia optimista con `rowversion` y ETag.
- Endpoints para administrar edificios, unidades, contratos y técnicos.
- Revisar los planes de ejecución con un volumen de datos realista.

## Uso de herramientas de IA
Se utilizó un asistente IA (Claude) para poder tomar generar datos de prueba y de igual manera usarlo como validador de funcionamiento del correcto uso de la API.
Se usó como guía durante la fase de pruebas. Hubo que hacer corrección de la logica en como se construian las reglas ya que una regla no se cumplia al 100%.