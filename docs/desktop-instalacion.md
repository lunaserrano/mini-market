# MiniMarket Desktop — Instalación local (offline)

Guía para instalar MiniMarket en una tienda **sin depender de internet**. Todo el sistema va en un
solo programa: lo único externo que necesita es **SQL Server**.

```
┌──────────────────────────────────────────────────────┐   SQL (login     ┌──────────────────────┐
│ MiniMarket.Desktop.exe                               │ ───────────────► │ SQL Server Express   │
│  ┌───────────────┐  HTTP+JWT   ┌──────────────────┐  │  minimarket_api) │ base "MiniMarket"    │
│  │ Pantallas     │ ──────────► │ MiniMarket.Api   │  │                  └──────────────────────┘
│  │ (WinForms)    │ 127.0.0.1:* │ (embebida)       │  │
│  └───────────────┘             └──────────────────┘  │
└──────────────────────────────────────────────────────┘
      un solo proceso: la Api arranca y se detiene con la ventana
```

- **No hay servicio Windows**: la Api (`MiniMarket.Api`, la misma de la versión web: reglas de
  negocio, permisos, validaciones y auditoría) corre **dentro** de `MiniMarket.Desktop.exe`, en el
  entorno `Desktop`. Escucha solo en `127.0.0.1` con un puerto elegido al azar al abrir la app: no
  es accesible desde la red y no hay puertos que configurar.
- Al abrirse, la app aplica las migraciones de la base (DbUp) y el seed inicial, igual que hacía el
  servicio.
- La conexión con SQL Server y la clave JWT se guardan **cifradas con DPAPI de máquina** en
  `%ProgramData%\MiniMarket\appsettings.Secrets.json`: solo se pueden descifrar en ese equipo. El
  refresh token ("Recordar sesión") se cifra con DPAPI del usuario de Windows.
- Los binarios se distribuyen **ofuscados** (Obfuscar) y **self-contained**: el equipo de la tienda
  no necesita tener .NET instalado.

---

## Requisitos

| Componente | Versión | Notas |
|---|---|---|
| Windows | 10/11 o Server 2016+ (x64) | |
| SQL Server Express | 2019 o 2022 | Gratis. Instancia `SQLEXPRESS`, **autenticación mixta** |
| sqlcmd | 16+ | Viene con SQL Server / SSMS. Solo para crear la base (paso 3) |
| .NET SDK 9 | 9.0.x | **Solo en la PC donde se compila** (`publish.ps1`) |

---

## Paso 1 — Compilar el instalable (PC de desarrollo)

Desde la raíz del repositorio:

```powershell
powershell -ExecutionPolicy Bypass -File desktop\install\publish.ps1
```

Genera `dist\` con:

| Carpeta | Contenido |
|---|---|
| `dist\app` | `MiniMarket.Desktop.exe` con la Api embebida, ofuscado y **sin** credenciales |
| `dist\tools` | `MiniMarket.ConfigTool.exe` (opcional: configurar la conexión por script) |
| `dist\install` | `install.ps1`, `uninstall.ps1`, `backup-db.ps1`, `00_setup_local.sql` y esta guía |

El script falla si en `dist\app` aparece cualquier `appsettings.*.json` o si `appsettings.json`
trae connection string o `Jwt:Key`. `-SinOfuscar` genera binarios sin ofuscar (útil para
diagnosticar). El mapa de la ofuscación (para leer stack traces) queda en
`desktop\MiniMarket.Desktop\obj\obfuscar\Mapping.txt`: **no se distribuye**.

Copie la carpeta `dist` completa al equipo de la tienda (USB, red...).

## Paso 2 — Instalar SQL Server Express (equipo de la tienda)

1. Descargue *SQL Server 2022 Express* e instale con la opción **Básica** o **Personalizada**.
2. Nombre de instancia: `SQLEXPRESS`.
3. Modo de autenticación: **Mixto** (Windows + SQL Server). Si ya estaba instalado en modo solo
   Windows: SSMS → propiedades del servidor → Seguridad → "Autenticación de SQL Server y Windows",
   y reinicie el servicio `SQL Server (SQLEXPRESS)`.

## Paso 3 — Crear la base y el login

Como administrador, en `dist\install`:

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -b -i 00_setup_local.sql `
       -v DbName="MiniMarket" ApiLogin="minimarket_api" ApiPassword="UnaClaveLarga#2026"
```

- Las **tres** variables son obligatorias.
- Crea la base (recuperación SIMPLE, `READ_COMMITTED_SNAPSHOT`) y el login `minimarket_api`.
  Si la base ya existía, no modifica su configuración.
- **No** crea tablas: la app aplica `database/migrations/*.sql` automáticamente al abrirse (DbUp),
  así la base local siempre tiene el mismo esquema que la central.

## Paso 4 — Instalar MiniMarket

PowerShell **como administrador**, en `dist\install`:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

El script:

1. Copia la app a `C:\Program Files\MiniMarket\App` y las herramientas a `...\Tools`.
2. Crea `%ProgramData%\MiniMarket` (donde se guarda la conexión cifrada) con permiso de escritura
   para los usuarios del equipo, para que la conexión se pueda configurar desde la app.
3. Registra el origen `MiniMarket` en el Visor de eventos.
4. Crea el acceso directo **MiniMarket POS** en el Escritorio público y en el menú Inicio.

Opciones:

| Parámetro | Efecto |
|---|---|
| `-SqlServer ".\SQLEXPRESS" [-Database MiniMarket] [-SqlUser minimarket_api]` | Configura la conexión durante la instalación (pide la contraseña de forma segura) |
| `-Integrada` | Con `-SqlServer`: usa autenticación de Windows en vez de usuario SQL |
| `-RestringirConfiguracion` | Solo los administradores podrán cambiar la conexión después (úselo junto con `-SqlServer`) |

**Actualizar**: vuelva a ejecutar `install.ps1` con el nuevo `dist`. Cierra la app si está
abierta, reemplaza los archivos y conserva la conexión configurada.

**Desde la versión anterior (con servicio `MiniMarketApi`)**: `install.ps1` elimina el servicio, la
regla de firewall y las carpetas `Api`/`Desktop`, y **conserva la conexión** que ya estaba
configurada (se copia a `%ProgramData%\MiniMarket`).

## Paso 5 — Primer ingreso

1. Abra **MiniMarket POS**. Si la conexión no se configuró en el paso 4, aparece
   *Conexión con la base de datos*: servidor (`.\SQLEXPRESS`), base (`MiniMarket`), usuario
   (`minimarket_api`) y contraseña → *Probar conexión* → *Guardar*.
2. La primera vez tarda unos segundos más: aplica las migraciones y crea la empresa demo, la
   sucursal, los roles y el usuario `admin`.
3. El login debe indicar "● Base de datos conectada". Usuario `admin`, contraseña `Admin123!`.
4. **Cambie la contraseña de inmediato**: Sistema → Cambiar contraseña.
5. Administración → Configuración de la empresa: nombre, NIT, moneda e IVA (salen en el ticket).
6. Administración → Roles y permisos / Usuarios: cree los cajeros.
7. Operación → Caja → *Abrir caja* y comience a vender (F9 abre el punto de venta).

La conexión se puede cambiar luego desde el login (*Configurar conexión...*) o Sistema → Conexión
(requiere reiniciar la app).

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

En `C:\Program Files\MiniMarket\App\appsettings.json`:

```json
"Ticket": { "AnchoMm": 80, "Impresora": "POS-80" }
```

Si `Impresora` está vacío, se usa la impresora predeterminada de Windows. Para papel de 58 mm, use `58`.

---

## Varias cajas en la red local

Cada caja lleva su propia copia completa de MiniMarket y **todas se conectan al mismo SQL Server**
(ya no hay una Api central en la red que pueda caerse: si una caja se apaga, las demás siguen).

1. En la PC que tiene SQL Server, habilite el acceso en red:
   - *SQL Server Configuration Manager* → Protocolos de SQLEXPRESS → **TCP/IP: Habilitado**, y
     reinicie el servicio de SQL Server.
   - Inicie el servicio **SQL Server Browser** (tipo de inicio: Automático) para usar el nombre de
     instancia (`PC-SERVIDOR\SQLEXPRESS`).
   - Firewall (perfiles Privado/Dominio): permita `sqlservr.exe` y el puerto UDP 1434 (Browser).
2. Asigne IP fija (o un nombre estable) a esa PC.
3. En cada caja ejecute `install.ps1`, por ejemplo:
   ```powershell
   .\install.ps1 -SqlServer "PC-SERVIDOR\SQLEXPRESS" -RestringirConfiguracion
   ```
   o configure la conexión al abrir la app por primera vez.

No exponga SQL Server a internet.

---

## Respaldos

Sin nube, el respaldo es la única protección ante una falla de disco. Programe un respaldo diario
en la PC de SQL Server, idealmente a un USB o NAS:

```powershell
schtasks /Create /SC DAILY /ST 22:00 /RU SYSTEM /TN "MiniMarket Backup" `
  /TR "powershell -ExecutionPolicy Bypass -File \"C:\Program Files\MiniMarket\Tools\backup-db.ps1\" -Carpeta D:\Respaldos"
```

`backup-db.ps1` conserva los últimos 14 días. Para restaurar: cierre MiniMarket en **todas** las
cajas y restaure el `.bak` con SSMS.

---

## Solución de problemas

| Síntoma | Causa / solución |
|---|---|
| Al abrir: "No se pudo iniciar MiniMarket" | SQL Server detenido o datos de conexión incorrectos. *Detalle técnico* muestra el error exacto; *Configurar conexión...* permite corregirlos y *Reintentar* vuelve a intentar |
| "SQL Server no disponible" (barra de estado) | Se perdió la conexión con SQL Server después de abrir la app. Al volver SQL Server, la app se recupera sola |
| "No puede escribir en C:\ProgramData\MiniMarket" al guardar la conexión | Se instaló con `-RestringirConfiguracion`: configure la conexión como administrador o con `install.ps1 -SqlServer ...` |
| Pide la conexión otra vez | `appsettings.Secrets.json` se copió desde otra PC o se dañó. DPAPI es por equipo: vuelva a configurarla |
| Errores inesperados | Visor de eventos → Registros de Windows → Aplicación, origen `MiniMarket` |
| Sesión expira seguido | El access token dura 15 min y se renueva solo. Si se revocan las sesiones del usuario (o se cambia su contraseña) debe volver a ingresar |

Comandos útiles:

```powershell
& "C:\Program Files\MiniMarket\Tools\MiniMarket.ConfigTool.exe" verify
Get-EventLog -LogName Application -Source MiniMarket -Newest 20
```

Desinstalar (no borra la base de datos): `dist\install\uninstall.ps1` (`-BorrarConfiguracion`
también elimina la conexión guardada).

---

## Desarrollo

```powershell
# 1. Base local de desarrollo
sqlcmd -S . -E -C -b -i database\desktop\00_setup_local.sql -v DbName="MiniMarketDev" ApiLogin="minimarket_dev" ApiPassword="Dev#Local2026"

# 2a. Opción A: conexión por variables de entorno (no escribe nada en ProgramData)
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=MiniMarketDev;User Id=minimarket_dev;Password=Dev#Local2026;TrustServerCertificate=True"
$env:Jwt__Key = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }) -as [byte[]])
dotnet run --project desktop\MiniMarket.Desktop

# 2b. Opción B: ejecutar la app y configurar la conexión en el diálogo del primer arranque
#     (queda cifrada en %ProgramData%\MiniMarket, como en una tienda)
```

La Api sigue pudiendo ejecutarse sola para la versión web (`dotnet run --project backend\src\MiniMarket.Api`);
el arranque compartido está en `backend/src/MiniMarket.Api/ApiHost.cs`.

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
   `Sync:Enabled=true` y `Sync:AzureApiUrl` en `App\appsettings.json`.
6. **Con varias cajas**, cada app corre su propio `SyncBackgroundService`: antes de habilitarlo,
   asegurar que un solo nodo sincronice (p. ej. un lock con `sp_getapplock` sobre la base compartida).

Sin conexión, el ciclo falla en silencio y reintenta: la tienda sigue vendiendo con normalidad.
