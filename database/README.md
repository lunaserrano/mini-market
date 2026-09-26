# Base de datos — MiniMarket

## Fuente de verdad

Este proyecto usa **Dapper** (no Entity Framework), así que no hay migraciones generadas
automáticamente desde el código. La fuente de verdad del esquema son los scripts SQL en
[`migrations/`](migrations/), escritos a mano y aplicados en orden por **DbUp**
(`MiniMarket.Infrastructure.Persistence.DatabaseMigrator`), que los embebe como recurso del
ensamblado y los ejecuta al iniciar `MiniMarket.Api` (ver `Program.cs`). DbUp registra en una
tabla `SchemaVersions` qué scripts ya se aplicaron, para no re-ejecutarlos.

Para agregar un cambio de esquema: crear un nuevo archivo `NNNN_descripcion.sql` en
`migrations/` (numeración correlativa, ej. `0018_agrega_traslados.sql`) — nunca editar un
script ya aplicado en algún ambiente. Al arrancar la Api en cualquier ambiente, los scripts
pendientes se aplican automáticamente.

`seed.sql` es un espejo de referencia (sin el hash de contraseña) de los datos que realmente
siembra `DataSeeder.cs` en desarrollo (empresa/sucursal demo, roles de sistema con sus permisos, usuario `admin` / `Admin123!`,
categorías base). El seed real corre solo en `Development` y es idempotente.

## Todo el acceso a datos pasa por stored procedures del esquema `market`

Desde la migración `0007` en adelante, el esquema de aplicación es **`market`** (antes `dbo`), y
**ningún repositorio de `MiniMarket.Infrastructure.Persistence.Repositories` ejecuta SQL de texto
contra las tablas**: todas las lecturas y escrituras pasan por stored procedures (`market.usp_*`).
Esto es deliberado, por seguridad:

- Reduce la superficie de inyección SQL: los parámetros de un procedure están tipados y fijos: no
  hay forma de concatenar SQL controlado por el usuario dentro de un procedure.
- Permite otorgarle a la Api un usuario de base de datos que **solo puede ejecutar procedures**
  (`GRANT EXECUTE`), sin `SELECT/INSERT/UPDATE/DELETE` directo sobre ninguna tabla — ver
  [`schema/market/13_security_users.sql`](schema/market/13_security_users.sql). Aunque hubiera un
  bug o una fuga de credenciales, ese usuario no podría leer ni escribir una tabla salvo a través
  de la superficie que exponen los procedures.

### Estructura

```text
database/
  migrations/            Historial que aplica DbUp en cada arranque de la Api (ver arriba).
  schema/market/         Paquete AUTOCONTENIDO para crear la base desde cero: mismo estado "de
                         llegada" que las migraciones, pero sin depender de su historial. Pensado
                         para aprovisionar un ambiente nuevo, CI, o para que alguien audite el
                         esquema y los procedures de un vistazo.
  seed.sql               Documentación de referencia del seed de desarrollo (ver DataSeeder.cs).
```

### `database/migrations` — lo que aplica la Api sola (incluye la base de Azure ya desplegada)

| Migración | Qué hace |
| --- | --- |
| `0001`-`0006*` | Esquema original en `dbo` (ver "Correcciones aplicadas..." más abajo). |
| `0007_market_schema_transfer.sql` | Crea el esquema `market` y mueve (`ALTER SCHEMA ... TRANSFER`) cada tabla de `dbo` a `market` sin perder datos ni recrear tablas. |
| `0008_market_types.sql` | Table types (TVP) que usan los procedures para recibir listas (códigos de permiso, lotes de auditoría). |
| `0009`-`0016_market_procedures_*.sql` | Todos los stored procedures, un archivo por módulo (catálogos, productos, inventario, caja, ventas, compras, créditos, seguridad). |
| `0017_market_seed_catalogo_permisos.sql` | Siembra/actualiza el catálogo de permisos vía `market.usp_Permiso_SincronizarLote`. |
| `0018_parametros.sql` | Tabla `market.Parametro` (llave EmpresaId+SucursalId) con la bandera para activar/desactivar la auditoría por empresa. |

Estas migraciones corren solas la próxima vez que arranque la Api (incluida la base de Azure SQL
configurada en `appsettings.Development.json`): no hace falta ejecutarlas a mano.

### `database/schema/market` — para crear una base nueva desde cero

Ejecutar con `sqlcmd` en orden (o todo junto con `run_all.sql`, que usa `:r` para incluirlos):

```bash
sqlcmd -S <servidor> -d MiniMarket -U <usuario-admin> -P <password> -i schema/market/run_all.sql
```

1. `00_create_database.sql` — crea la base (solo SQL Server local/on-prem; en Azure SQL la base ya
   existe como recurso aprovisionado, se omite este paso).
2. `01_schema_and_tables.sql` — esquema `market` + las 24 tablas, con todas sus FKs e índices.
3. `02_types.sql` — table types (TVP).
4. `03`-`10_procedures_*.sql` — todos los stored procedures, por módulo.
5. `11_seed_catalogo_permisos.sql` — catálogo de permisos (idempotente).
6. `12_seed_demo.sql` — empresa/sucursal/roles/categorías de ejemplo (opcional; el usuario admin
   real lo crea la Api en el primer arranque en Development, porque necesita calcular su hash BCrypt).
7. `13_security_users.sql` — usuario de aplicación `market_app` de mínimo privilegio (opcional pero
   **recomendado siempre en producción**: cambia la contraseña placeholder antes de correrlo).

### Usuario de aplicación de mínimo privilegio

`13_security_users.sql` crea `market_app`, que solo puede `EXECUTE` sobre el esquema `market`
(nada de `SELECT/INSERT/UPDATE/DELETE` directo, por "ownership chaining" al estar procedures y
tablas en el mismo esquema). Para usarlo, cambia la connection string de la Api de la cuenta
administradora a este usuario — pero **no lo pongas en un `appsettings.*.json` versionado**; usa
variables de entorno, `dotnet user-secrets`, o Azure Key Vault.

> ⚠️ **`appsettings.Development.json` tiene actualmente una contraseña real de Azure SQL en texto
> plano y está en git** (`ConnectionStrings:DefaultConnection`, servidor
> `luna-pos.database.windows.net`). Si ese archivo ya se subió a un repositorio remoto, esa
> contraseña debe considerarse comprometida: rota la contraseña de esa cuenta en Azure y mueve la
> connection string a `dotnet user-secrets` (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`
> desde `backend/src/MiniMarket.Api`) o a una variable de entorno, nunca a un archivo versionado.
> Esto es independiente de la migración a `market`/stored procedures y conviene resolverlo aparte.

### Convención para nuevas queries

Cualquier query nueva que agregue la Api debe ir en un stored procedure de `market`, nunca como
SQL embebido en un repositorio. Al llamarlo desde Dapper, pasa siempre un objeto (anónimo o
`DynamicParameters`) con **exactamente** los parámetros que declara el procedure — a diferencia de
`CommandType.Text`, `CommandType.StoredProcedure` hace que SQL Server rechace con el error 8144
cualquier parámetro de más (por ejemplo, pasar una entidad completa que traiga columnas como `Id`
que el procedure de creación no declara).

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
- **`Permiso`** es un catálogo global con códigos `modulo.accion` (`ventas.anular`, `usuarios.crear`...). La fuente de verdad es `Domain/Security/Permisos.cs`: `PermisoCatalogSync` lo sincroniza en **cada arranque** (todos los entornos) llamando a `market.usp_Permiso_SincronizarLote`, así que agregar un permiso no exige otra migración. **`RolPermiso`** une rol y permiso.
- **El rol Administrador no tiene filas en `RolPermiso`**: siempre tiene todo el catálogo (no puede quedarse sin acceso). Los permisos por defecto de supervisor y cajero replican lo que hacían los `[Authorize(Roles)]` anteriores; para empresas nuevas los asigna `DataSeeder` desde `Permisos.PorDefecto` — **mantener ambos sincronizados** si se cambian.
- **`Usuario`** gana `IntentosFallidos`/`BloqueadoHasta` (bloqueo temporal tras 5 intentos fallidos, configurable en la sección `Seguridad` de appsettings), `DebeCambiarPassword` (contraseña temporal: hasta cambiarla solo puede usar los endpoints de sesión), `UltimoLoginUtc` y `PasswordCambiadaUtc`.
- **`RefreshToken`** guarda solo el **hash SHA-256** del token. Cada uso lo rota (misma `FamiliaId`); presentar uno ya rotado revoca toda la familia. El access token (JWT) dura 15 minutos y lleva los permisos como claims `permiso`, por eso un cambio de permisos o de rol llega al usuario al renovar su sesión (máximo 15 minutos).
- **`EventoSeguridad`** es la auditoría (logins, bloqueos, cambios de usuarios/roles/permisos). Sin FK a propósito: el registro sobrevive aunque se borre el usuario o el rol.

## Auditoría de toda la actividad (`0006_auditoria_actividad.sql`)

`EventoSeguridad` deja de ser solo de seguridad y pasa a registrar **toda** la actividad, distinguida por la columna `Origen`:

- **`SEG`** — eventos de seguridad de siempre (login, bloqueos, cambios de usuarios/roles).
- **`API`** — cada petición HTTP al backend (`AuditoriaMiddleware`): método, ruta con query, código de estado, duración, usuario, IP y cuerpo JSON con contraseñas/tokens ocultos (`Datos`). Se omiten los preflights `OPTIONS`, Swagger y `POST /api/auditoria/cliente`.
- **`UI`** — clics y cambios de pantalla que el navegador reporta por lotes a `POST /api/auditoria/cliente`. `Datos` guarda el descriptor del elemento (nunca el valor de un campo).

Las filas de `API` y `UI` no se escriben en la petición: pasan por una cola en memoria y un `BackgroundService` las inserta por lotes (`market.usp_EventoSeguridad_RegistrarLote`, vía table-valued parameter). Con esta granularidad la tabla crece rápido: conviene definir una retención (purga por `FechaUtc`) antes de producción.

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

## Esquema `market` y stored procedures (`0007`-`0017`)

Ver la sección ["Todo el acceso a datos pasa por stored procedures del esquema `market`"](#todo-el-acceso-a-datos-pasa-por-stored-procedures-del-esquema-market) arriba: mueve todas las
tablas de `dbo` a `market` y reemplaza el SQL embebido de los repositorios por `market.usp_*`.

## Parámetros por empresa/sucursal y bandera de auditoría (`0018_parametros.sql`)

`market.Parametro` es una tabla de configuración con llave primaria compuesta `(EmpresaId, SucursalId)`
(`SucursalId = 0` = valor por defecto de toda la empresa; una sucursal puntual puede tener su propio
override). Primer parámetro que guarda: `AuditoriaHabilitada`, la bandera para apagar el registro de
auditoría (`market.EventoSeguridad`, ver `0006_auditoria_actividad.sql`) sin tocar código ni
appsettings, cuando la tabla crece demasiado.

- `market.usp_Parametro_ObtenerAuditoriaHabilitada` prioriza la fila de la sucursal puntual y cae al
  valor de empresa (`SucursalId = 0`) si no hay override; si no existe ninguna fila, el repositorio
  (`ParametroRepository`) asume **habilitada** (fail-safe: nunca se deja de auditar por una fila
  faltante, hay que apagarla explícitamente).
- `market.usp_Parametro_ActualizarAuditoriaHabilitada` hace upsert de la bandera.
- La Api cachea el resultado por ~30s (`AuditoriaEstadoProvider`) para no sumar una consulta a BD en
  cada petición auditada — apagar/prender la auditoría de una empresa tarda hasta ese margen en
  reflejarse. `AuditoriaMiddleware`, `AuditoriaService` (eventos de UI) y `SeguridadAuditor` (eventos
  de seguridad) consultan este estado antes de encolar/escribir. El interruptor global de
  `appsettings` (sección `Auditoria`) sigue existiendo como apagado de emergencia para todas las
  empresas a la vez, sin depender de que la BD responda.
- `GET/PUT /api/auditoria/estado` (permiso `empresa.editar`) expone la bandera de la empresa actual
  (`SucursalId = 0`) para administrarla desde Configuración.

## Motor de base de datos usado en desarrollo

La connection string activa en `backend/src/MiniMarket.Api/appsettings.Development.json` apunta
hoy a una base de **Azure SQL Database** (`luna-pos.database.windows.net`). Si tu máquina no tiene
acceso a esa base, usa SQL Server local, LocalDB (`(localdb)\MSSQLLocalDB`) o un contenedor Docker,
y actualiza esa connection string (o mejor, sobrescríbela con `dotnet user-secrets` en vez de
editar el archivo versionado — ver la advertencia sobre la contraseña más arriba).
