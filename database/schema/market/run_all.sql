-- ============================================================
-- run_all.sql
-- Crea la base de datos MiniMarket completa desde cero: esquema market, tablas, tipos, TODOS los
-- stored procedures, catálogo de permisos, datos demo y usuario de aplicación de mínimo privilegio.
--
-- Ejecutar con sqlcmd (los :r solo funcionan con sqlcmd o con "Modo SQLCMD" en SSMS, no pegando el
-- archivo directo en una ventana de consulta normal):
--
--   sqlcmd -S <servidor> -d MiniMarket -U <usuario-admin> -P <password> -i run_all.sql
--
-- En Azure SQL Database: crea la base desde Portal/CLI/Terraform, omite 00_create_database.sql, y
-- apunta -d al nombre real de tu base de datos.
--
-- 12_seed_demo.sql y 13_security_users.sql son opcionales (comenta la línea :r correspondiente si no
-- los quieres): el primero siembra datos de ejemplo, el segundo crea un login de aplicación separado
-- del administrador. En producción se recomienda ejecutar 13 SIEMPRE y usar ese usuario en la
-- connection string de la Api en vez de la cuenta administradora.
-- ============================================================

--:r 00_create_database.sql
:r 01_schema_and_tables.sql
:r 02_types.sql
:r 03_procedures_catalogos.sql
:r 04_procedures_productos.sql
:r 05_procedures_inventario.sql
:r 06_procedures_caja.sql
:r 07_procedures_ventas.sql
:r 08_procedures_compras.sql
:r 09_procedures_creditos.sql
:r 10_procedures_seguridad.sql
:r 14_procedures_parametros.sql
:r 11_seed_catalogo_permisos.sql
:r 12_seed_demo.sql
:r 13_security_users.sql

PRINT 'MiniMarket: base de datos creada en el esquema market.';
GO
