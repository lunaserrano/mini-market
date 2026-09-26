-- ============================================================
-- 11_seed_catalogo_permisos.sql
-- Siembra el catálogo global de permisos (tabla market.Permiso), reflejando el catálogo definido en
-- código (backend/src/MiniMarket.Domain/Security/Permisos.cs — fuente de verdad). En la Api esto lo
-- hace PermisoCatalogSync en cada arranque llamando a market.usp_Permiso_SincronizarLote; este script
-- deja la base utilizable de inmediato aunque la Api todavía no haya arrancado una vez.
-- Idempotente: se puede volver a ejecutar sin duplicar filas ni pisar Id existentes.
-- ============================================================

DECLARE @Permisos market.PermisoListType;
INSERT INTO @Permisos (Codigo, Modulo, Nombre) VALUES
    ('ventas.ver',              'Ventas',           'Ver ventas'),
    ('ventas.crear',            'Ventas',           'Registrar ventas'),
    ('ventas.anular',           'Ventas',           'Anular ventas'),
    ('ventas.ver_todas',        'Ventas',           'Ver las ventas de todos los usuarios'),

    ('caja.operar',             'Caja',             'Abrir, cerrar y registrar movimientos de caja'),
    ('caja.ver_todas',          'Caja',             'Ver las cajas de todos los usuarios'),

    ('productos.ver',           'Productos',        'Ver productos'),
    ('productos.gestionar',     'Productos',        'Crear y editar productos y precios'),
    ('productos.eliminar',      'Productos',        'Desactivar productos'),

    ('categorias.ver',          'Categorías',       'Ver categorías'),
    ('categorias.gestionar',    'Categorías',       'Crear y editar categorías'),
    ('categorias.eliminar',     'Categorías',       'Desactivar categorías'),

    ('clientes.ver',            'Clientes',         'Ver clientes'),
    ('clientes.gestionar',      'Clientes',         'Crear y editar clientes'),
    ('clientes.eliminar',       'Clientes',         'Desactivar clientes'),

    ('proveedores.ver',         'Proveedores',      'Ver proveedores'),
    ('proveedores.gestionar',   'Proveedores',      'Crear y editar proveedores'),
    ('proveedores.eliminar',    'Proveedores',      'Desactivar proveedores'),

    ('creditos.ver',            'Créditos',         'Ver créditos y sus abonos'),
    ('creditos.abonar',         'Créditos',         'Registrar abonos a créditos'),
    ('creditos.otorgar',        'Créditos',         'Vender a crédito'),

    ('inventario.consultar',    'Inventario',       'Consultar existencias de un producto'),
    ('inventario.ver',          'Inventario',       'Ver inventario y movimientos'),
    ('inventario.ajustar',      'Inventario',       'Ajustar existencias'),

    ('compras.ver',             'Compras',          'Ver compras'),
    ('compras.crear',           'Compras',          'Registrar compras'),
    ('compras.anular',          'Compras',          'Anular compras'),

    ('empresa.editar',          'Configuración',    'Editar la configuración de la empresa'),

    ('usuarios.ver',                 'Usuarios',    'Ver usuarios'),
    ('usuarios.crear',               'Usuarios',    'Crear usuarios'),
    ('usuarios.editar',              'Usuarios',    'Editar usuarios y revocar sus sesiones'),
    ('usuarios.cambiar_estado',      'Usuarios',    'Activar y desactivar usuarios'),
    ('usuarios.reset_password',      'Usuarios',    'Restablecer contraseñas'),
    ('usuarios.desbloquear',         'Usuarios',    'Desbloquear cuentas'),

    ('roles.ver',                'Roles y permisos', 'Ver roles y permisos'),
    ('roles.gestionar',          'Roles y permisos', 'Crear, editar y eliminar roles'),

    ('auditoria.ver',            'Auditoría',        'Consultar la auditoría de seguridad');

EXEC market.usp_Permiso_SincronizarLote @Permisos;
GO
