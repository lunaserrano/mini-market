# Base de datos — MiniMarket

## Fuente de verdad

Este proyecto usa **Dapper** (no Entity Framework), así que no hay migraciones generadas
automáticamente desde el código. La fuente de verdad del esquema son los scripts SQL en
[`migrations/`](migrations/), escritos a mano y aplicados en orden por **DbUp**
(`MiniMarket.Infrastructure.Persistence.DatabaseMigrator`), que los embebe como recurso del
ensamblado y los ejecuta al iniciar `MiniMarket.Api` (ver `Program.cs`). DbUp registra en una
tabla `SchemaVersions` qué scripts ya se aplicaron, para no re-ejecutarlos.

Para agregar un cambio de esquema: crear un nuevo archivo `NNNN_descripcion.sql` en
`migrations/` (numeración correlativa, ej. `0002_agrega_traslados.sql`) — nunca editar un
script ya aplicado en algún ambiente. Al arrancar la Api en cualquier ambiente, los scripts
pendientes se aplican automáticamente.

`seed.sql` es un espejo de referencia (sin el hash de contraseña) de los datos que realmente
siembra `DataSeeder.cs` en desarrollo (empresa/sucursal demo, roles, usuario `admin` / `Admin123!`,
categorías base). El seed real corre solo en `Development` y es idempotente.

## Correcciones aplicadas sobre el script original del usuario

El diseño original compartido para revisión tenía 12 problemas de fondo, todos corregidos en
`0001_initial.sql`:

1. **`Inventario` sin `SucursalId`** → ahora es `(ProductoId, SucursalId)` con constraint único; el stock se lleva por sucursal, no globalmente.
2. **`Producto` atado a una sola sucursal** → ahora pertenece a la `Empresa` (catálogo compartido); el stock por sucursal vive en `Inventario`.
3. **`MovimientoInventario` sin `EmpresaId`/`SucursalId`/`UsuarioId`** y con `referencia` como texto libre → se agregan esas columnas más `DocumentoOrigenTipo`/`DocumentoOrigenId` (referencia estructurada) y `StockResultante` (saldo, para auditoría).
4. **Sin tabla de ingresos/egresos de caja** (solo apertura/cierre) → nueva tabla `MovimientoCaja`.
5. **Texto libre sin catálogo/constraint** (`Usuario.rol`, `Caja.estado`, `Pago.metodo`, `MovimientoInventario.tipo`) → catálogo `RolCatalogo` + `CHECK` constraints.
6. **Sin tabla `Cliente`** (el frontend ya la modelaba) → agregada.
7. **`TipoPrecio.CantidadBase` era `INT`** → `DECIMAL(18,4)` (soporta productos a granel); índice único filtrado garantiza un solo `EsDefault=1` por producto.
8. **Sin folios ni estado de anulación** en Venta/Compra, sin desglose de subtotal/descuento/impuesto → agregados `Folio` (correlativo por sucursal), `Estado` (`COMPLETADA`/`ANULADA`), `Subtotal`/`DescuentoTotal`/`ImpuestoTotal`/`Total`.
9. **Sin índices en `EmpresaId`/`SucursalId`** (SQL Server no indexa FKs automáticamente) → agregados en todas las tablas tenant-scoped.
10. **`GETDATE()` sin considerar UTC** → todas las fechas transaccionales son `datetime2` en UTC (`SYSUTCDATETIME()`); `Empresa.ZonaHoraria` guarda la zona para presentación.
11. **Sin columnas de auditoría** ni patrón consistente de estado → catálogos usan `Estado CHAR(1)` `'A'`/`'I'`; documentos transaccionales usan `Estado` `'COMPLETADA'`/`'ANULADA'` (nunca se mezclan).
12. **FKs sin `ON DELETE` explícito** → todas `NO ACTION`; nunca cascade sobre datos transaccionales (se usa soft delete vía `Estado`).

## Moneda configurable por empresa (`0002_moneda_empresa.sql`)

La moneda no está fija en el código: `Empresa` tiene `CodigoMoneda` (ISO 4217) y `SimboloMoneda`,
editables desde **Configuración** (`GET/PUT /api/empresa`, solo admin) sin tocar código ni
releases. Default de columna: `USD`/`$` — El Salvador usa dólar estadounidense como moneda
oficial. El frontend lee este valor una vez por sesión (`ConfigService`) y lo aplica en toda la
app vía el pipe `moneda`, así que cambiarlo desde Configuración se refleja de inmediato sin
tocar plantillas.

## IVA por empresa, precios con impuesto incluido (`0003_iva_empresa.sql`)

`Producto.TasaImpuesto` se eliminó: el IVA ahora es un único mantenimiento en `Empresa.TasaImpuesto`
(editable en Configuración), no algo que se ingrese por producto. Además, `TipoPrecio.PrecioVenta`
cambió de significado: **ya es el precio final con IVA incluido** (lo que se cobra tal cual en el
POS), no un precio base al que se le suma el impuesto encima. `VentaService.CrearAsync` desglosa
`Subtotal`/`ImpuestoTotal` "hacia adentro" a partir de ese precio y la tasa de la empresa, en vez de
sumarlo — así el total que el cajero ve en pantalla (que siempre fue `Σ precioVenta × cantidad`) es
exactamente el mismo que valida el backend, sin desfases al cobrar.

## Multi-tenant sin Row-Level Security (por ahora)

Todas las tablas relevantes llevan `EmpresaId`/`SucursalId` con índice explícito, pensadas para
que cada repositorio Dapper filtre siempre por esos valores (ver `ITenantContext` en
`MiniMarket.Application`). No se usa Row-Level Security de SQL Server en este alcance — es una
evolución futura documentada como defensa adicional, no un requisito del esqueleto actual.

## Motor de base de datos usado en desarrollo

Se detectó una instancia de **SQL Server 2022 Developer Edition** corriendo localmente
(`Server=localhost`) con autenticación integrada de Windows — ver la connection string en
`backend/src/MiniMarket.Api/appsettings.Development.json`. Si tu máquina no tiene SQL Server
instalado, usa LocalDB (`(localdb)\MSSQLLocalDB`) o un contenedor Docker, y actualiza esa
connection string (o mejor, sobrescríbela con `dotnet user-secrets`).
