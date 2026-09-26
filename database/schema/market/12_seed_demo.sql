-- ============================================================
-- 12_seed_demo.sql
-- Siembra de datos de referencia para un ambiente nuevo (empresa demo, sucursal, roles de sistema y
-- categorías iniciales), usando los mismos stored procedures que usa la Api — nunca INSERT directo,
-- siguiendo la misma política del resto de la base.
--
-- Nota T-SQL: los procedures devuelven el Id creado con "OUTPUT INSERTED.Id" (un result set), no con
-- RETURN, así que para capturarlo en una variable de script hay que volcar ese result set a una tabla
-- temporal (INSERT ... EXEC) en vez de "EXEC @Variable = procedure" (eso solo leería un código de
-- retorno). Dapper, en cambio, sí lee el result set directamente con QuerySingleAsync<int> — por eso
-- el código C# de la Api no necesita este paso intermedio.
--
-- El usuario admin NO se crea aquí: su PasswordHash es un hash BCrypt que solo la Api sabe calcular
-- (ver backend/src/MiniMarket.Infrastructure/Persistence/DataSeeder.cs, que se ejecuta automáticamente
-- en entorno Development si market.Empresa está vacía). Para crear el usuario admin manualmente contra
-- una base ya sembrada con este script, genera un hash BCrypt de la contraseña deseada y ejecuta:
--
--   DECLARE @RolAdminId INT = (SELECT Id FROM market.RolCatalogo WHERE EmpresaId = 1 AND Codigo = 'admin');
--   EXEC market.usp_Usuario_Crear
--        @EmpresaId = 1, @SucursalId = 1, @RolId = @RolAdminId,
--        @NombreCompleto = N'Administrador General', @Username = N'admin',
--        @PasswordHash = N'<hash-bcrypt-aqui>', @Estado = 'A', @DebeCambiarPassword = 0,
--        @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME();
--
-- Idempotente a nivel de intención (no vuelvas a correrlo sobre una base que ya tiene datos: crearía
-- una segunda empresa demo). Ejecuta solo una vez, contra una base recién creada con 01-11.
-- ============================================================

DECLARE @EmpresaId INT, @SucursalId INT, @RolAdminId INT;
DECLARE @PermisosVacio market.CodigoListType, @PermisosSupervisor market.CodigoListType, @PermisosCajero market.CodigoListType;
DECLARE @IdTabla TABLE (Id INT);

-- CodigoMoneda/SimboloMoneda/TasaImpuesto toman su DEFAULT de columna (USD/$/13%) — El Salvador usa
-- dólar estadounidense e IVA del 13%. Cambia esto desde Configuración (GET/PUT /api/empresa), no aquí.
INSERT INTO @IdTabla (Id)
EXEC market.usp_Empresa_Crear
     @Nombre = N'Mini Market Demo', @RazonSocial = N'Mini Market Demo, S.A. de C.V.',
     @ZonaHoraria = N'America/El_Salvador', @Estado = 'A';
SELECT TOP 1 @EmpresaId = Id FROM @IdTabla;
DELETE FROM @IdTabla;

INSERT INTO @IdTabla (Id)
EXEC market.usp_Sucursal_Crear
     @EmpresaId = @EmpresaId, @Nombre = N'Sucursal Principal', @Direccion = N'San Salvador', @Estado = 'A';
SELECT TOP 1 @SucursalId = Id FROM @IdTabla;
DELETE FROM @IdTabla;

-- Roles de sistema (EsSistema = 1). El admin no lleva filas en RolPermiso: siempre tiene todo el
-- catálogo (ver market.usp_Rol_Listar). Los permisos de supervisor/cajero replican
-- Domain/Security/Permisos.cs → PorDefecto.
INSERT INTO @PermisosSupervisor (Codigo) VALUES
    ('ventas.ver'),('ventas.crear'),('ventas.anular'),('ventas.ver_todas'),
    ('caja.operar'),('caja.ver_todas'),
    ('productos.ver'),('productos.gestionar'),
    ('categorias.ver'),('categorias.gestionar'),
    ('clientes.ver'),('clientes.gestionar'),
    ('creditos.ver'),('creditos.abonar'),('creditos.otorgar'),
    ('proveedores.ver'),('proveedores.gestionar'),
    ('inventario.consultar'),('inventario.ver'),('inventario.ajustar'),
    ('compras.ver'),('compras.crear'),('compras.anular');

INSERT INTO @PermisosCajero (Codigo) VALUES
    ('ventas.ver'),('ventas.crear'),
    ('caja.operar'),
    ('creditos.ver'),('creditos.abonar'),('creditos.otorgar'),
    ('productos.ver'),
    ('inventario.consultar');

INSERT INTO @IdTabla (Id)
EXEC market.usp_Rol_Crear
     @EmpresaId = @EmpresaId, @Codigo = 'admin', @Nombre = N'Administrador',
     @Descripcion = N'Acceso total al sistema. No editable.', @EsSistema = 1, @Estado = 'A',
     @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME(), @Permisos = @PermisosVacio;
SELECT TOP 1 @RolAdminId = Id FROM @IdTabla;
DELETE FROM @IdTabla;

EXEC market.usp_Rol_Crear
     @EmpresaId = @EmpresaId, @Codigo = 'supervisor', @Nombre = N'Supervisor',
     @Descripcion = N'Gestión operativa: catálogo, inventario, compras y anulaciones.', @EsSistema = 1, @Estado = 'A',
     @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME(), @Permisos = @PermisosSupervisor;

EXEC market.usp_Rol_Crear
     @EmpresaId = @EmpresaId, @Codigo = 'cajero', @Nombre = N'Cajero',
     @Descripcion = N'Punto de venta y operación de caja.', @EsSistema = 1, @Estado = 'A',
     @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME(), @Permisos = @PermisosCajero;

EXEC market.usp_Categoria_Crear @EmpresaId = @EmpresaId, @Nombre = N'Abarrotes', @Descripcion = N'Productos de abarrotes en general', @Estado = 'A', @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME();
EXEC market.usp_Categoria_Crear @EmpresaId = @EmpresaId, @Nombre = N'Bebidas', @Descripcion = N'Bebidas embotelladas y enlatadas', @Estado = 'A', @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME();
EXEC market.usp_Categoria_Crear @EmpresaId = @EmpresaId, @Nombre = N'Limpieza', @Descripcion = N'Artículos de limpieza del hogar', @Estado = 'A', @CreadoPorUsuarioId = NULL, @FechaCreacion = SYSUTCDATETIME();

SELECT @EmpresaId AS EmpresaId, @SucursalId AS SucursalId, @RolAdminId AS RolAdminId;
GO
