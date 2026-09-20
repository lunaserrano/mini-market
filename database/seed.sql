-- ============================================================
-- seed.sql — referencia/documentación del seed de desarrollo.
-- La siembra REAL la ejecuta MiniMarket.Infrastructure.Persistence.DataSeeder al iniciar la Api
-- en entorno Development (única y exclusivamente ahí se calcula el hash BCrypt de la contraseña
-- del usuario admin). Este script es un espejo de referencia sin el password hash.
-- Usuario admin de desarrollo: username "admin", password "Admin123!".
-- ============================================================

-- CodigoMoneda/SimboloMoneda toman su DEFAULT de columna ('USD'/'$') — El Salvador usa dólar
-- estadounidense. Cambia la moneda desde Configuración (GET/PUT /api/empresa), no aquí.
INSERT INTO Empresa (Nombre, RazonSocial, ZonaHoraria, Estado)
VALUES ('Mini Market Demo', 'Mini Market Demo, S.A. de C.V.', 'America/El_Salvador', 'A');

INSERT INTO Sucursal (EmpresaId, Nombre, Direccion, Estado)
VALUES (1, 'Sucursal Principal', 'San Salvador', 'A');

-- Roles de sistema por empresa (EsSistema = 1). El admin no lleva filas en RolPermiso: siempre tiene todo el catálogo.
-- Los permisos por defecto de supervisor y cajero (RolPermiso) los asigna DataSeeder desde Domain/Security/Permisos.cs
-- (Permisos.PorDefecto) una vez que PermisoCatalogSync ha poblado la tabla Permiso al arrancar la Api.
INSERT INTO RolCatalogo (EmpresaId, Codigo, Nombre, Descripcion, EsSistema) VALUES
(1, 'admin', 'Administrador', 'Acceso total al sistema. No editable.', 1),
(1, 'supervisor', 'Supervisor', 'Gestión operativa: catálogo, inventario, compras y anulaciones.', 1),
(1, 'cajero', 'Cajero', 'Punto de venta y operación de caja.', 1);

-- Usuario admin: ver DataSeeder.cs para el hash real generado con BCrypt en tiempo de ejecución.
-- INSERT INTO Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado)
-- VALUES (1, 1, 1, 'Administrador General', 'admin', '<bcrypt-hash>', 'A');   -- RolId = Id del rol admin de la empresa

INSERT INTO Categoria (EmpresaId, Nombre, Descripcion, Estado) VALUES
(1, 'Abarrotes', 'Productos de abarrotes en general', 'A'),
(1, 'Bebidas', 'Bebidas embotelladas y enlatadas', 'A'),
(1, 'Limpieza', 'Artículos de limpieza del hogar', 'A');
