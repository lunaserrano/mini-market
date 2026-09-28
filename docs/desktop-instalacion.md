# MiniMarket Desktop — Instalación local (offline)

Guía para instalar MiniMarket en una tienda **sin depender de internet**: la base de datos, la Api y
el cliente de escritorio corren en la misma PC (o en una PC "servidor" de la red local).

```
┌──────────────────────┐   HTTP + JWT    ┌───────────────────────────┐   SQL (login    ┌──────────────────────┐
│ MiniMarket.Desktop   │ ──────────────► │ MiniMarket.Api            │ ──────────────► │ SQL Server Express   │
│ (WinForms, cada caja)│  127.0.0.1:5080 │ (servicio "MiniMarketApi")│  minimarket_api)│ base "MiniMarket"    │
└──────────────────────┘                 └───────────────────────────┘                 └──────────────────────┘
        sin acceso a la BD                   toda la lógica de negocio                     solo la Api se conecta
                                                     │
                                                     └── (futuro) SyncBackgroundService ──► Api central en Azure
```

- El cliente WinForms **nunca** se conecta a SQL Server: solo consume la Api por HTTP.
- La Api es la misma que usa la versión web (mismas reglas de negocio, permisos y auditoría), en
  el entorno `Desktop`.
- Los secretos (connection string, clave JWT) se guardan **cifrados con DPAPI** y solo se pueden
  descifrar en el equipo donde se generaron. El refresh token del cliente ("Recordar sesión") se
  cifra con DPAPI del usuario de Windows.
- Los binarios de la Api y del cliente se distribuyen **ofuscados** (Obfuscar).

---

## Requisitos

| Componente | Versión | Notas |
|---|---|---|
| Windows | 10/11 o Server 2016+ (x64) | |
| SQL Server Express | 2019 o 2022 | Gratis. Instancia `SQLEXPRESS`, **autenticación mixta** |
| sqlcmd | 16+ | Viene con SQL Server / SSMS |
| .NET SDK 9 | 9.0.x | **Solo en la PC donde se compila** (`publish.ps1`). El equipo de la tienda NO lo necesita: los instalables son self-contained |

---

## Paso 1 — Compilar los instalables (PC de desarrollo)

Desde la raíz del repositorio:

```powershell
powershell -ExecutionPolicy Bypass -File desktop\install\publish.ps1
```

Genera `dist\` con:

| Carpeta | Contenido |
|---|---|
| `dist\api` | Api (servicio Windows), ofuscada, **sin** credenciales de Azure ni `Jwt:Key` |
| `dist\desktop` | Cliente WinForms ofuscado (`MiniMarket.Desktop.exe`) |
| `dist\tools` | `MiniMarket.ConfigTool.exe` (cifra los secretos en el equipo destino) |
| `dist\install` | Scripts de instalación, `00_setup_local.sql` y esta guía |

`-SinOfuscar` genera binarios sin ofuscar (útil para diagnosticar). El mapa de nombres de la
ofuscación (para leer stack traces) queda en `obj\obfuscar\Mapping.txt` de cada proyecto: **no se
distribuye**.

Copie la carpeta `dist` completa al equipo de la tienda (USB, red...).

## Paso 2 — Instalar SQL Server Express (equipo de la tienda)

1. Descargue *SQL Server 2022 Express* e instale con la opción **Básica** o **Personalizada**.
2. Nombre de instancia: `SQLEXPRESS`.
3. Modo de autenticación: **Mixto** (Windows + SQL Server). Si ya estaba instalado en modo solo
   Windows: SSMS → propiedades del servidor → Seguridad → "Autenticación de SQL Server y Windows",
   y reinicie el servicio `SQL Server (SQLEXPRESS)`.

## Paso 3 — Crear la base y el login del servicio

Como administrador, en `dist\install`:

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -b -i 00_setup_local.sql `
       -v DbName="MiniMarket" ApiLogin="minimarket_api" ApiPassword="UnaClaveLarga#2026"
```

- Las **tres** variables son obligatorias.
- Crea la base (recuperación SIMPLE, `READ_COMMITTED_SNAPSHOT`) y el login `minimarket_api`.
  Si la base ya existía, no modifica su configuración.
- **No** crea tablas: la Api aplica `database/migrations/*.sql` automáticamente al arrancar (DbUp),
  así la base local siempre tiene el mismo esquema que la central.

## Paso 4 — Instalar el servicio y el cliente

PowerShell **como administrador**, en `dist\install`:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-api-service.ps1 -SqlServer ".\SQLEXPRESS" -Database MiniMarket -SqlUser minimarket_api
```

El script:

1. Copia la Api, el cliente y las herramientas a `C:\Program Files\MiniMarket\{Api,Desktop,Tools}`.
2. Pide la contraseña SQL y ejecuta `MiniMarket.ConfigTool init`. Este prueba la conexión y genera
   `Api\appsettings.Secrets.json` con la connection string y una `Jwt:Key` aleatoria **cifradas
   (DPAPI de máquina)**. El archivo solo lo pueden leer SYSTEM y los Administradores.
3. Registra el servicio `MiniMarketApi` con inicio automático, reinicio ante fallos y entorno
   `Desktop`.
4. Inicia el servicio. El primer arranque aplica las migraciones y crea la empresa demo, la
   sucursal, los roles y el usuario `admin`. Luego verifica `http://127.0.0.1:5080/api/health`.
5. Crea el acceso directo **MiniMarket POS** en el Escritorio público.

Si se vuelve a ejecutar para una **actualización**, conserva los secretos existentes.

## Paso 5 — Primer ingreso

1. Abra **MiniMarket POS**. La pantalla de login debe indicar "● Servicio local en línea".
2. Usuario `admin`, contraseña `Admin123!`.
3. **Cambie la contraseña de inmediato**: Sistema → Cambiar contraseña.
4. Administración → Configuración de la empresa: nombre, NIT, moneda e IVA (salen en el ticket).
5. Administración → Roles y permisos / Usuarios: cree los cajeros.
6. Operación → Caja → *Abrir caja* y comience a vender (F9 abre el punto de venta).

### Atajos del punto de venta

| Tecla | Acción |
|---|---|
| F2 | Ir al buscador (el lector de código de barras escribe el código + Enter) |
| F4 | Cobrar |
| Supr | Quitar la línea seleccionada |
| Esc | Limpiar la búsqueda |
| F5 | Refrescar la pantalla actual |
| F8 / F9 | Caja / Punto de venta |

### Impresora de tickets

En `C:\Program Files\MiniMarket\Desktop\appsettings.json`:

```json
"Ticket": { "AnchoMm": 80, "Impresora": "POS-80" }
```

Si `Impresora` está vacío, se usa la impresora predeterminada de Windows. Para papel de 58 mm, use `58`.

---

## Varias cajas en la red local

1. En la PC servidor, instale con acceso en red. El script abre el puerto 5080 en el firewall
   (perfiles Privado/Dominio):
   ```powershell
   .\install-api-service.ps1 -AccesoRed
   ```
2. En cada caja copie solo `dist\desktop`, abra la app → *Configurar conexión...* (o Sistema →
   Conexión) → `http://<IP-del-servidor>:5080/api/` → *Probar conexión* → Guardar.
3. Asigne IP fija al servidor. El tráfico es HTTP dentro de la LAN: no exponga el puerto 5080 a
   internet.

---

## Respaldos

Sin nube, el respaldo es la única protección ante una falla de disco. Programe un respaldo diario,
idealmente a un USB o NAS:

```powershell
schtasks /Create /SC DAILY /ST 22:00 /RU SYSTEM /TN "MiniMarket Backup" `
  /TR "powershell -ExecutionPolicy Bypass -File \"C:\Program Files\MiniMarket\Tools\backup-db.ps1\" -Carpeta D:\Respaldos"
```

`backup-db.ps1` conserva los últimos 14 días. Para restaurar: detenga el servicio `MiniMarketApi` y
restaure el `.bak` con SSMS.

---

## Solución de problemas

| Síntoma | Causa / solución |
|---|---|
| Login: "● Servicio local detenido" | `services.msc` → iniciar **MiniMarket Api (local)**. Si no arranca: Visor de eventos → Registros de Windows → Aplicación, origen `MiniMarketApi` |
| "SQL Server no disponible" | Servicio `SQL Server (SQLEXPRESS)` detenido, o la contraseña del login cambió → `Tools\MiniMarket.ConfigTool.exe verify --dir "C:\Program Files\MiniMarket\Api"` y, si falla, borrar `appsettings.Secrets.json` y reinstalar |
| "No se pudo descifrar 'ConnectionStrings:DefaultConnection'" | Se copió `appsettings.Secrets.json` desde otra PC. DPAPI es por equipo: vuelva a generarlo con `ConfigTool init` |
| Puerto 5080 ocupado | Reinstale con `-Puerto 5090` y cambie la URL en Sistema → Conexión |
| Sesión expira seguido | El access token dura 15 min y se renueva solo. Si se revocan las sesiones del usuario (o se cambia su contraseña) debe volver a ingresar |

Comandos útiles:

```powershell
Get-Service MiniMarketApi
Invoke-RestMethod http://127.0.0.1:5080/api/health
& "C:\Program Files\MiniMarket\Tools\MiniMarket.ConfigTool.exe" verify --dir "C:\Program Files\MiniMarket\Api"
```

Desinstalar (no borra la base de datos): `dist\install\uninstall-api-service.ps1`.

---

## Desarrollo (sin instalar el servicio)

```powershell
# 1. Base local de desarrollo
sqlcmd -S . -E -C -b -i database\desktop\00_setup_local.sql -v DbName="MiniMarketDev" ApiLogin="minimarket_dev" ApiPassword="Dev#Local2026"

# 2. Secretos cifrados junto al binario de la Api
dotnet build backend\MiniMarket.sln
dotnet run --project backend\tools\MiniMarket.ConfigTool -- init --dir backend\src\MiniMarket.Api\bin\Debug\net9.0 --server . --database MiniMarketDev --user minimarket_dev --password "Dev#Local2026"

# 3. Api en modo Desktop (desde su carpeta de salida, que es donde está appsettings.Secrets.json)
cd backend\src\MiniMarket.Api\bin\Debug\net9.0
$env:ASPNETCORE_ENVIRONMENT = "Desktop"; .\MiniMarket.Api.exe

# 4. Cliente
dotnet run --project desktop\MiniMarket.Desktop
```

`appsettings.Secrets.json` está en `.gitignore`: nunca se versiona.

---

## Hoja de ruta: sincronización con Azure

El esquema ya está preparado (migración `0019_sync_preparacion.sql`). Hoy la sincronización está
**apagada** (`Sync:Enabled=false`) y el sistema no hace ninguna llamada a internet.

| Pieza | Estado | Para qué |
|---|---|---|
| `SyncId UNIQUEIDENTIFIER` en tablas de negocio | ✅ | Identidad global de cada fila entre sucursales (los `Id` IDENTITY chocan entre nodos) |
| `SyncVersion ROWVERSION` | ✅ | Cambios incrementales "desde la versión X", sin tocar los procedures |
| `market.NodoSync` | ✅ | Identidad de la instalación y marcas de última sincronización |
| `market.SyncOutbox` + `usp_SyncOutbox_*` | ✅ | Cola de documentos pendientes de subir (patrón outbox) |
| `ISyncService` / `NoOpSyncService` / `SyncBackgroundService` | ✅ | Punto de extensión y ciclo periódico (Sync:IntervaloMinutos) |
| `AzureSyncService` (implementación real) | ⏳ | Pendiente |

Pasos para habilitarla:

1. **PUSH (documentos)**: en los procedures de venta, compra, caja y abono, llamar a
   `market.usp_SyncOutbox_Encolar` con el documento en JSON (`FOR JSON PATH`) dentro de la misma
   transacción. `AzureSyncService` lee `usp_SyncOutbox_ObtenerPendientes`, hace POST a la Api central
   (endpoint nuevo `/api/sync/push`, idempotente por `SyncId`) y marca `MarcarEnviado`/`MarcarError`.
2. **PULL (catálogos)**: la Api central expone `/api/sync/pull?desde=<SyncVersion>` con productos,
   precios, categorías y clientes. El nodo los aplica por `SyncId` (MERGE) y guarda la última
   versión en `NodoSync.UltimaVersionRecibida`.
3. **Conflictos**: los documentos transaccionales nunca se editan (solo se anulan), así que no
   generan conflicto. Para catálogos, la central es la autoridad (last-writer-wins desde la nube).
4. **Autenticación nodo → nube**: un client credential por nodo (Azure AD / clave de API), guardado
   cifrado con `ConfigTool encrypt` en `appsettings.Secrets.json` (`Sync:ClientSecret`).
5. Registrar `AzureSyncService` en lugar de `NoOpSyncService` (`DependencyInjection.cs`) y poner
   `Sync:Enabled=true` y `Sync:AzureApiUrl` en `appsettings.Desktop.json`.

Sin conexión, el ciclo falla en silencio y reintenta: la tienda sigue vendiendo con normalidad.
