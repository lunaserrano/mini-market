# Mini Market — POS + Backoffice

Sistema de punto de venta y backoffice para un minisúper, diseñado como SaaS
multiempresa/multisucursal desde el día uno (aunque el despliegue inicial es local, de una sola
empresa). Ver [`docs/architecture.md`](docs/architecture.md) para el diseño completo.

- **Backend**: `backend/` — .NET 9 Web API, capas Domain/Application/Infrastructure/Api, Dapper (no EF Core).
- **Frontend**: `frontend/` — Angular 22 + PrimeNG + Tailwind CSS.
- **Base de datos**: `database/` — scripts SQL versionados, aplicados por DbUp al iniciar la Api.

## Requisitos

- .NET SDK 9.0.x (fijado en `backend/global.json`)
- Node.js 22.x + npm 11.x
- SQL Server accesible (Developer/Express/LocalDB) — local o remoto

## 1. Base de datos

No hay que crear el esquema a mano: al iniciar la Api, `DatabaseMigrator` aplica automáticamente
los scripts de `database/migrations/` (vía DbUp) contra la base indicada en la connection string.
Solo hace falta que la base **exista** y esté vacía la primera vez:

```powershell
sqlcmd -S localhost -Q "CREATE DATABASE MiniMarketDb"
```

Ajusta `ConnectionStrings:DefaultConnection` en
`backend/src/MiniMarket.Api/appsettings.Development.json` si tu instancia de SQL Server no es
`localhost` con autenticación integrada de Windows.

## 2. Backend

```powershell
cd backend
dotnet user-secrets set "Jwt:Key" "<una-clave-larga-y-aleatoria>" --project src/MiniMarket.Api
dotnet run --project src/MiniMarket.Api --urls http://localhost:5080
```

- Swagger: http://localhost:5080/swagger
- En `Development`, `DataSeeder` crea automáticamente (si la base está vacía): empresa y sucursal
  demo (moneda por defecto: **USD**, `$`), los 3 roles, categorías base, y un usuario administrador:
  - **Usuario**: `admin`
  - **Contraseña**: `Admin123!`
- La moneda es configurable por empresa (no está fija en el código): un admin puede cambiarla en
  **Configuración** dentro de la app, o vía `PUT /api/empresa` — útil si se despliega en otro país.

## 3. Frontend

```powershell
cd frontend
npm install
npm start   # ng serve, http://localhost:4200
```

El `apiUrl` de desarrollo (`frontend/src/environments/environment.ts`) apunta a
`http://localhost:5080/api`; para producción se reemplaza por `environment.prod.ts` vía
`fileReplacements` en `angular.json`.

## 4. Flujo de prueba sugerido

1. Login con `admin` / `Admin123!`.
2. Crear una categoría y un producto con al menos dos tipos de precio (ej. "Unidad" y "Six pack").
3. Ir a **Inventario** y hacer un ajuste manual para cargar stock inicial.
4. Ir a **Caja** y abrir turno.
5. Ir a **Punto de venta**, buscar el producto, agregarlo al carrito, cobrar con uno o más
   métodos de pago.
6. Volver a **Caja** y cerrar turno — el sistema calcula el monto esperado automáticamente.

## Estructura y decisiones de diseño

Ver [`docs/architecture.md`](docs/architecture.md) (arquitectura completa, multi-tenant sin EF,
transacciones con Dapper, endpoints) y [`database/README.md`](database/README.md) (modelo de
datos y correcciones aplicadas sobre el diseño original).
