# Arquitectura — MiniMarket (POS + Backoffice multi-tenant)

Sistema para un minisúper, diseñado como SaaS multiempresa/multisucursal desde el día uno
(`EmpresaId`/`SucursalId` en toda entidad relevante), aunque el despliegue inicial es de una
sola empresa/sucursal, uso local.

## 1. Stack

| Capa | Tecnología |
|---|---|
| Frontend | Angular 22 (standalone, signals) + PrimeNG 22 (tema Aura) + Tailwind CSS 3.4 |
| Backend | .NET 9 Web API, Clean Architecture por capas |
| Acceso a datos | **Dapper** (no EF Core) + SQL parametrizado explícito |
| Migraciones | **DbUp** sobre scripts SQL versionados en `database/migrations/` |
| Auth | JWT (HMAC-SHA256) + BCrypt para contraseñas |
| Base de datos | SQL Server |

## 2. Monorepo

```
mini-market/
├── backend/            # Solución .NET (MiniMarket.sln)
│   └── src/
│       ├── MiniMarket.Domain          # Entidades POCO, enums, excepciones de dominio
│       ├── MiniMarket.Application     # DTOs, interfaces (repos/servicios), Services (reglas de negocio)
│       ├── MiniMarket.Infrastructure  # Dapper, DbUp, JWT, BCrypt, TenantContext, DI
│       └── MiniMarket.Api             # Controllers, Program.cs, appsettings
├── frontend/            # Angular 22 + PrimeNG + Tailwind
│   └── src/app/
│       ├── core/        # interceptors, guards, services HTTP, models
│       ├── layout/      # shell (sidebar + topbar) filtrado por rol
│       └── features/    # una carpeta por módulo de negocio
├── database/
│   ├── migrations/      # *.sql — fuente de verdad del esquema (aplicados por DbUp)
│   ├── seed.sql          # espejo de referencia del seed de desarrollo
│   └── README.md         # catálogo de correcciones al modelo de datos original
└── docs/architecture.md  # este documento
```

## 3. Multi-tenant sin EF (el punto crítico del diseño)

Sin *global query filters* de EF Core, el aislamiento por `EmpresaId`/`SucursalId` depende de
disciplina explícita en cada repositorio Dapper:

1. Todo método de repositorio que toca una entidad tenant-scoped recibe `empresaId` (y
   `sucursalId` cuando aplica) como **parámetro obligatorio**, nunca opcional, y lo usa siempre
   en el `WHERE`/`INSERT`.
2. Los `Service` de `Application` obtienen esos valores **siempre** de `ITenantContext`
   (implementado en `Infrastructure/Services/TenantContext.cs`, que lee los claims del JWT
   `empresa_id`/`sucursal_id`/`usuario_id`/`rol`) — nunca del body que envía el cliente, para que
   nadie pueda forzar el `EmpresaId` de otra empresa.
3. No se usa Row-Level Security de SQL Server en este alcance (evolución futura documentada);
   el modelo de columnas/índices ya queda preparado para agregarla sin romper nada.

## 4. Transacciones sin EF (`IUnitOfWork`)

Sin `SaveChanges`, cada operación que debe ser atómica (el caso más sensible: crear una venta —
descuenta stock, inserta movimiento de inventario, inserta venta+detalle+pagos) corre dentro de
una única transacción ADO.NET explícita (`Infrastructure/Persistence/UnitOfWork.cs`). Si no se
llama `Commit()`, el `Dispose()` revierte automáticamente. Ver `VentaService.CrearAsync` como
referencia del patrón.

## 5. Modelo de datos

Ver [`database/README.md`](../database/README.md) para el detalle de las 12 correcciones
aplicadas sobre el script original y el esquema completo en
[`database/migrations/0001_initial.sql`](../database/migrations/0001_initial.sql).

Entidades: `Empresa`, `Sucursal`, `RolCatalogo`, `Usuario`, `Categoria`, `Proveedor`, `Cliente`,
`Producto` (+ `TipoPrecio`: presentaciones con `CantidadBase` para conversión de unidades),
`Inventario` (por sucursal), `MovimientoInventario`, `Caja`, `MovimientoCaja`, `Venta` +
`DetalleVenta` + `PagoVenta` (pagos múltiples), `Compra` + `DetalleCompra`.

## 6. Endpoints (por módulo, roles requeridos)

| Módulo | Endpoints | Roles |
|---|---|---|
| Auth | `POST /api/auth/login`, `GET /api/auth/me` | público / todos |
| Productos | `GET /api/productos`, `GET /api/productos/buscar`, CRUD `/api/productos/{id}`, CRUD `/api/productos/{id}/tipos-precio` | admin, supervisor (lectura: +cajero) |
| Categorías | CRUD `/api/categorias` | admin, supervisor |
| Inventario | `GET /api/inventario`, `GET /api/inventario/{productoId}/sucursal/{sucursalId}`, `POST /api/inventario/ajuste`, `GET /api/inventario/movimientos` | admin, supervisor (+cajero en consulta puntual) |
| Proveedores | CRUD `/api/proveedores` | admin, supervisor |
| Compras | `GET/POST /api/compras`, `POST /api/compras/{id}/anular` | admin, supervisor |
| Caja | `GET /api/caja/actual`, `POST /api/caja/apertura`, `POST /api/caja/{id}/cierre`, `GET/POST /api/caja/{id}/movimientos` | admin, supervisor, cajero |
| Ventas | `POST /api/ventas`, `GET /api/ventas`, `GET /api/ventas/{id}`, `POST /api/ventas/{id}/anular` | admin, supervisor, cajero (anular: admin/supervisor) |
| Clientes | CRUD `/api/clientes` | admin, supervisor |
| Usuarios | CRUD `/api/usuarios`, estado, reset password | admin |

Swagger disponible en `/swagger` en Development, con soporte de Bearer token.

## 7. Flujo del POS (venta)

1. Login → JWT con claims `empresa_id/sucursal_id/usuario_id/rol`.
2. La ruta `/pos` exige caja abierta (`GET /api/caja/actual`); si no hay, redirige a `/caja`.
3. Búsqueda de producto por nombre o código de barras (`GET /api/productos/buscar`, con debounce).
4. Si el producto tiene más de una presentación (`TipoPrecio`), se elige una (Unidad, Docena,
   Mayoreo, Granel...); cada una trae `CantidadBase` (factor de conversión a unidad base) y
   `PrecioVenta`.
5. Carrito en memoria en el frontend; el backend es la única autoridad de stock/precio real.
6. Pago: admite N líneas (efectivo/tarjeta/transferencia) que deben cubrir el total.
7. `POST /api/ventas`, dentro de una transacción (`VentaService.CrearAsync`):
   valida caja → por línea calcula `cantidadBase = cantidad × TipoPrecio.CantidadBase`, bloquea
   la fila de `Inventario` (`UPDLOCK, ROWLOCK`), valida stock, descuenta, inserta
   `MovimientoInventario` con `StockResultante` → calcula folio correlativo por sucursal y
   totales (subtotal/descuento/impuesto) → valida que los pagos cubran el total → inserta
   Venta + Detalle + Pagos → si hay efectivo, registra `MovimientoCaja` INGRESO para que el
   corte de caja cuadre → commit.
8. Cierre de turno: `MontoFinalSistema = MontoInicial + Σingresos − Σegresos`, comparado contra
   lo declarado por el cajero (`Diferencia`).

## 8. Decisiones abiertas / evolución futura

- **Impuestos**: ~~tasa por producto~~ — resuelto: es un mantenimiento único en `Empresa.TasaImpuesto`
  (Configuración, solo admin, default 13% para El Salvador). `TipoPrecio.PrecioVenta` es el precio
  final con IVA incluido; el backend lo desglosa "hacia adentro", no lo suma encima (ver
  `database/README.md`).
- **Reversión de stock al anular** venta/compra: el endpoint de anulación marca el documento
  como `ANULADA` pero no revierte automáticamente el `MovimientoInventario` — pendiente para una
  siguiente iteración (ver comentario en `VentaService.AnularAsync`/`CompraService.AnularAsync`).
- **Row-Level Security** de SQL Server: no implementada, el modelo ya está listo para agregarla.
- **Traslados entre sucursales**: el enum `TipoMovimientoInventario` ya contempla
  `TrasladoEntrada`/`TrasladoSalida`, pero no hay endpoint todavía.
- **Testing automatizado**: no incluido en este alcance; se recomienda al menos una prueba de
  integración del flujo de venta end-to-end antes de producción.
