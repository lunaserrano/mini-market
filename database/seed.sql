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

INSERT INTO RolCatalogo (Codigo, Nombre) VALUES
('admin', 'Administrador'),
('supervisor', 'Supervisor'),
('cajero', 'Cajero');

-- Usuario admin: ver DataSeeder.cs para el hash real generado con BCrypt en tiempo de ejecución.
-- INSERT INTO Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado)
-- VALUES (1, 1, 1, 'Administrador General', 'admin', '<bcrypt-hash>', 'A');

INSERT INTO Categoria (EmpresaId, Nombre, Descripcion, Estado) VALUES
(1, 'Abarrotes', 'Productos de abarrotes en general', 'A'),
(1, 'Bebidas', 'Bebidas embotelladas y enlatadas', 'A'),
(1, 'Limpieza', 'Artículos de limpieza del hogar', 'A');
