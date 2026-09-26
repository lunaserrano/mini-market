-- ============================================================
-- 00_create_database.sql
-- Crea la base de datos MiniMarket desde cero (solo SQL Server on-prem/local; en Azure SQL la base
-- ya existe como recurso aprovisionado y este paso se omite: conéctate directamente a ella y sigue
-- con 01_schema_and_tables.sql en adelante).
--
-- Uso (sqlcmd, SQL Server local o de instancia):
--   sqlcmd -S <servidor> -d master -i 00_create_database.sql
--
-- Para Azure SQL Database: crea la base desde Portal/CLI/Terraform (el "CREATE DATABASE" de aquí no
-- aplica a Azure SQL Database con el mismo alcance) y ejecuta el resto de los scripts contra ella.
-- ============================================================

IF DB_ID(N'MiniMarket') IS NULL
BEGIN
    CREATE DATABASE MiniMarket;
END
GO
