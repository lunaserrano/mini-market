-- ============================================================
-- 00_setup_local.sql — Script SQL inicial del modo Desktop (offline, SQL Server Express)
--
-- Crea la base de datos local y el login con el que se conecta MiniMarket (la Api embebida en MiniMarket.Desktop).
-- Las TABLAS y STORED PROCEDURES no se crean aquí: la app aplica database/migrations/*.sql con DbUp
-- en cada arranque (DatabaseMigrator.ApplyMigrations), así la base local siempre queda en la misma
-- versión de esquema que la central de Azure. Luego el seed (Seed:Enabled) crea empresa, sucursal,
-- roles y el usuario admin.
--
-- Ejecutar como administrador de SQL Server (sa o un login sysadmin de Windows):
--   sqlcmd -S .\SQLEXPRESS -E -C -b -i 00_setup_local.sql ^
--          -v DbName="MiniMarket" ApiLogin="minimarket_api" ApiPassword="UnaClaveFuerte#2026"
--
-- Las TRES variables son obligatorias (sqlcmd aborta si falta alguna). No se usan ":setvar" con
-- valores por defecto a propósito: en sqlcmd un :setvar del script PISA el -v de la línea de
-- comandos, y el script terminaría operando sobre otra base distinta de la indicada.
-- Idempotente: puede ejecutarse varias veces (actualiza la contraseña del login si ya existe).
-- ============================================================

SET NOCOUNT ON;
GO

-- 1. Base de datos. Recuperación SIMPLE (una caja no suele tener backups de log: evita que el .ldf
--    crezca sin límite; los respaldos completos se programan aparte, ver docs/desktop-instalacion.md)
--    y READ_COMMITTED_SNAPSHOT (evita bloqueos lector/escritor entre el POS y los reportes).
--    Solo se aplican al CREAR la base: una base existente nunca se modifica.
IF DB_ID(N'$(DbName)') IS NULL
BEGIN
    PRINT N'Creando base de datos $(DbName)...';
    CREATE DATABASE [$(DbName)];
    ALTER DATABASE [$(DbName)] SET RECOVERY SIMPLE;
    ALTER DATABASE [$(DbName)] SET READ_COMMITTED_SNAPSHOT ON;
END
ELSE
    PRINT N'La base de datos $(DbName) ya existe: no se modifica su configuración.';
GO

-- 2. Login SQL del servicio (autenticación mixta debe estar habilitada en la instancia)
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(ApiLogin)')
BEGIN
    PRINT N'Creando login $(ApiLogin)...';
    CREATE LOGIN [$(ApiLogin)] WITH PASSWORD = N'$(ApiPassword)', DEFAULT_DATABASE = [$(DbName)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
END
ELSE
BEGIN
    PRINT N'El login $(ApiLogin) ya existe: se actualiza su contraseña.';
    ALTER LOGIN [$(ApiLogin)] WITH PASSWORD = N'$(ApiPassword)';
END
GO

-- 3. Usuario en la base. db_owner porque DbUp crea/altera tablas, tipos y procedures al migrar.
--    El cliente WinForms NUNCA usa este login: solo la Api (el cliente habla HTTP con la Api).
USE [$(DbName)];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(ApiLogin)')
    CREATE USER [$(ApiLogin)] FOR LOGIN [$(ApiLogin)];
GO

IF IS_ROLEMEMBER(N'db_owner', N'$(ApiLogin)') = 0
    ALTER ROLE db_owner ADD MEMBER [$(ApiLogin)];
GO

PRINT N'Listo. Siguiente paso: MiniMarket.ConfigTool init --user $(ApiLogin) --password ... (ver docs/desktop-instalacion.md)';
GO
