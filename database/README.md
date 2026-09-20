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
siembra `DataSeeder.cs` en desarrollo (empresa/sucursal demo, roles de sistema con sus permisos, usuario `admin` / `Admin123!`,
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

## Compras usan el mismo catálogo de presentaciones que Ventas (`0004_detallecompra_tipoprecio.sql`)

`DetalleCompra` ya no recibía ningún factor de conversión validado: el cliente escribía a mano un
`CantidadBase` libre por línea. Ahora `DetalleCompra.TipoPrecioId` referencia una presentación real
del producto (el mismo catálogo `TipoPrecio` que usan las Ventas — Unidad, Cartón x30, Caja x360...),
resuelta de forma autoritativa en `CompraService.CrearAsync` exactamente igual que ya hacía
`VentaService.CrearAsync`. Columna nullable a propósito (compras históricas antes de este cambio
quedan sin presentación asociada, sin backfill inventado). Cada compra además deja
`TipoPrecio.PrecioCompra` de esa presentación actualizado con el último precio pagado (dentro de la
misma transacción), para que la siguiente compra ya sugiera el costo más reciente.

## Seguridad: roles por empresa, permisos, sesiones y auditoría (`0005_seguridad.sql`)

Reemplaza los 3 roles fijos y el `[Authorize(Roles = ...)]` por **permisos** que el administrador asigna a **roles editables**:

- **`RolCatalogo` es ahora por empresa** (`EmpresaId`). Los 3 roles base (`admin`/`supervisor`/`cajero`) se copian a cada empresa como `EsSistema = 1` (no se pueden eliminar); el admin crea el resto desde **Roles y permisos**. La migración copia los roles globales a cada empresa existente, repunta `Usuario.RolId` y elimina los globales. El `UNIQUE` sobre `Codigo` (nombre autogenerado) se reemplaza por `UX_RolCatalogo_Empresa_Codigo`.
- **`Permiso`** es un catálogo global con códigos `modulo.accion` (`ventas.anular`, `usuarios.crear`...). La fuente de verdad es `Domain/Security/Permisos.cs`: `PermisoCatalogSync` lo sincroniza en **cada arranque** (todos los entornos), así que agregar un permiso no exige otra migración. **`RolPermiso`** une rol y permiso.
- **El rol Administrador no tiene filas en `RolPermiso`**: siempre tiene todo el catálogo (no puede quedarse sin acceso). Los permisos por defecto de supervisor y cajero replican lo que hacían los `[Authorize(Roles)]` anteriores; para empresas nuevas los asigna `DataSeeder` desde `Permisos.PorDefecto` — **mantener ambos sincronizados** si se cambian.
- **`Usuario`** gana `IntentosFallidos`/`BloqueadoHasta` (bloqueo temporal tras 5 intentos fallidos, configurable en la sección `Seguridad` de appsettings), `DebeCambiarPassword` (contraseña temporal: hasta cambiarla solo puede usar los endpoints de sesión), `UltimoLoginUtc` y `PasswordCambiadaUtc`.
- **`RefreshToken`** guarda solo el **hash SHA-256** del token. Cada uso lo rota (misma `FamiliaId`); presentar uno ya rotado revoca toda la familia. El access token (JWT) dura 15 minutos y lleva los permisos como claims `permiso`, por eso un cambio de permisos o de rol llega al usuario al renovar su sesión (máximo 15 minutos).
- **`EventoSeguridad`** es la auditoría (logins, bloqueos, cambios de usuarios/roles/permisos). Sin FK a propósito: el registro sobrevive aunque se borre el usuario o el rol.

## Auditoría de toda la actividad (`0006_auditoria_actividad.sql`)

`EventoSeguridad` deja de ser solo de seguridad y pasa a registrar **toda** la actividad, distinguida por la columna `Origen`:

- **`SEG`** — eventos de seguridad de siempre (login, bloqueos, cambios de usuarios/roles).
- **`API`** — cada petición HTTP al backend (`AuditoriaMiddleware`): método, ruta con query, código de estado, duración, usuario, IP y cuerpo JSON con contraseñas/tokens ocultos (`Datos`). Se omiten los preflights `OPTIONS`, Swagger y `POST /api/auditoria/cliente`.
- **`UI`** — clics y cambios de pantalla que el navegador reporta por lotes a `POST /api/auditoria/cliente`. `Datos` guarda el descriptor del elemento (nunca el valor de un campo).

Las filas de `API` y `UI` no se escriben en la petición: pasan por una cola en memoria y un `BackgroundService` las inserta por lotes. Con esta granularidad la tabla crece rápido: conviene definir una retención (purga por `FechaUtc`) antes de producción.

## Multi-tenant sin Row-Level Security (por ahora)

Todas las tablas relevantes llevan `EmpresaId`/`SucursalId` con índice explícito, pensadas para
que cada repositorio Dapper filtre siempre por esos valores (ver `ITenantContext` en
`MiniMarket.Application`). No se usa Row-Level Security de SQL Server en este alcance — es una
evolución futura documentada como defensa adicional, no un requisito del esqueleto actual.

## Ventas a crédito (`0006_creditos.sql`)

Un cliente puede llevarse una venta sin pagarla del todo y abonar después hasta cancelarla.

- **`Credito`**: una fila por venta a crédito (índice único sobre `VentaId`). `MontoOriginal` es lo que
  quedó a deber al vender (`Venta.Total` − pagos recibidos en el momento, que siguen en `PagoVenta`);
  `SaldoPendiente` baja con cada abono. `CHECK (SaldoPendiente <= MontoOriginal)`.
- **`AbonoCredito`**: cada pago posterior. Inmutable (no se edita ni se borra). Si es en efectivo se
  liga a la `Caja` abierta que lo recibió y además genera un `INGRESO` en `MovimientoCaja`
  (`DocumentoReferenciaTipo = 'AbonoCredito'`), así el corte de caja cuadra.
- **Estados**: `PENDIENTE` → `PAGADO` (saldo 0) | `ANULADO` (se anuló la venta origen). "Vencido" no se
  guarda: se calcula al consultar (`PENDIENTE` con `FechaVencimiento` pasada). La fecha de vencimiento se
  guarda al final del día elegido.
- **Anular una venta a crédito** solo se permite mientras no tenga abonos; con abonos ya hay dinero del
  cliente recibido y la devolución no está modelada.
- **Permisos** `creditos.ver`, `creditos.abonar`, `creditos.otorgar` (vender a crédito, se valida en
  `VentaService`). La migración se los asigna a `supervisor` y `cajero` de las empresas existentes; las
  nuevas los reciben del `DataSeeder`. `GET /api/clientes` también acepta `creditos.otorgar` para que el
  POS pueda elegir cliente.

## Motor de base de datos usado en desarrollo

Se detectó una instancia de **SQL Server 2022 Developer Edition** corriendo localmente
(`Server=localhost`) con autenticación integrada de Windows — ver la connection string en
`backend/src/MiniMarket.Api/appsettings.Development.json`. Si tu máquina no tiene SQL Server
instalado, usa LocalDB (`(localdb)\MSSQLLocalDB`) o un contenedor Docker, y actualiza esa
connection string (o mejor, sobrescríbela con `dotnet user-secrets`).
