-- ============================================================
-- 04_procedures_productos.sql
-- Stored procedures de market para Producto y TipoPrecio. Sustituyen el SQL embebido de
-- ProductoRepository (Infrastructure/Persistence). Los listados que antes armaban un IN (@ids) desde
-- C# ahora resuelven todo server-side con result sets múltiples (Dapper: QueryMultipleAsync) o
-- variables de tabla locales, sin necesitar table-valued parameters.
-- ============================================================

-- Result set 1: productos de la empresa. Result set 2: categorías de la empresa (para el nombre).
-- Result set 3: tipos de precio de esos productos.
CREATE OR ALTER PROCEDURE market.usp_Producto_Listar
    @EmpresaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Producto WHERE EmpresaId = @EmpresaId ORDER BY Nombre;

    SELECT Id, Nombre FROM market.Categoria WHERE EmpresaId = @EmpresaId;

    SELECT tp.*
    FROM market.TipoPrecio tp
    INNER JOIN market.Producto p ON p.Id = tp.ProductoId
    WHERE p.EmpresaId = @EmpresaId
    ORDER BY tp.Nombre;
END
GO

-- Result set 1: producto + nombre de categoría. Result set 2: sus tipos de precio.
CREATE OR ALTER PROCEDURE market.usp_Producto_ObtenerPorId
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.*, c.Nombre AS CategoriaNombre
    FROM market.Producto p
    LEFT JOIN market.Categoria c ON c.Id = p.CategoriaId
    WHERE p.EmpresaId = @EmpresaId AND p.Id = @Id;

    SELECT * FROM market.TipoPrecio WHERE ProductoId = @Id ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Producto_ObtenerEntidad
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Producto WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

-- Result set 1: hasta 20 productos activos que hacen match por código de barras o nombre, con su
-- stock en la sucursal. Result set 2: tipos de precio activos de justo esos productos (vía la
-- variable de tabla @Ids, sin exponer un TVP en la firma del procedure).
CREATE OR ALTER PROCEDURE market.usp_Producto_BuscarParaPos
    @EmpresaId INT, @SucursalId INT, @Termino NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Ids TABLE (Id INT PRIMARY KEY);

    INSERT INTO @Ids (Id)
    SELECT TOP 20 p.Id
    FROM market.Producto p
    WHERE p.EmpresaId = @EmpresaId AND p.Estado = 'A'
      AND (p.CodigoBarras = @Termino OR p.Nombre LIKE '%' + @Termino + '%')
    ORDER BY p.Nombre;

    SELECT p.Id, p.Nombre, p.CodigoBarras, p.UnidadBase, ISNULL(i.StockActual, 0) AS StockActual
    FROM market.Producto p
    INNER JOIN @Ids ids ON ids.Id = p.Id
    LEFT JOIN market.Inventario i ON i.ProductoId = p.Id AND i.SucursalId = @SucursalId
    ORDER BY p.Nombre;

    SELECT tp.*
    FROM market.TipoPrecio tp
    INNER JOIN @Ids ids ON ids.Id = tp.ProductoId
    WHERE tp.Estado = 'A'
    ORDER BY tp.Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Producto_Crear
    @EmpresaId INT, @CategoriaId INT, @ProveedorId INT, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500),
    @CodigoBarras NVARCHAR(100), @CodigoInterno NVARCHAR(50), @ImagenPath NVARCHAR(500), @UnidadBase NVARCHAR(20),
    @Estado CHAR(1), @CreadoPorUsuarioId INT, @FechaCreacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Producto (EmpresaId, CategoriaId, ProveedorId, Nombre, Descripcion, CodigoBarras, CodigoInterno,
        ImagenPath, UnidadBase, Estado, CreadoPorUsuarioId, FechaCreacion)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @CategoriaId, @ProveedorId, @Nombre, @Descripcion, @CodigoBarras, @CodigoInterno,
        @ImagenPath, @UnidadBase, @Estado, @CreadoPorUsuarioId, @FechaCreacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Producto_Actualizar
    @Id INT, @EmpresaId INT, @CategoriaId INT, @ProveedorId INT, @Nombre NVARCHAR(150), @Descripcion NVARCHAR(500),
    @CodigoBarras NVARCHAR(100), @CodigoInterno NVARCHAR(50), @ImagenPath NVARCHAR(500), @UnidadBase NVARCHAR(20),
    @ModificadoPorUsuarioId INT, @FechaModificacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Producto
    SET CategoriaId = @CategoriaId, ProveedorId = @ProveedorId, Nombre = @Nombre,
        Descripcion = @Descripcion, CodigoBarras = @CodigoBarras, CodigoInterno = @CodigoInterno,
        ImagenPath = @ImagenPath, UnidadBase = @UnidadBase,
        ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
    WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Producto_CambiarEstado
    @EmpresaId INT, @Id INT, @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Producto SET Estado = @Estado WHERE Id = @Id AND EmpresaId = @EmpresaId;
END
GO

-- ---------- TipoPrecio ----------

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_Listar
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.TipoPrecio WHERE ProductoId = @ProductoId ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_ObtenerPorId
    @ProductoId INT, @TipoPrecioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.TipoPrecio WHERE ProductoId = @ProductoId AND Id = @TipoPrecioId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_Crear
    @ProductoId INT, @Nombre NVARCHAR(50), @CantidadBase DECIMAL(18,4), @PrecioVenta DECIMAL(18,2),
    @PrecioCompra DECIMAL(18,2), @EsDefault BIT, @Estado CHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.TipoPrecio (ProductoId, Nombre, CantidadBase, PrecioVenta, PrecioCompra, EsDefault, Estado)
    OUTPUT INSERTED.Id
    VALUES (@ProductoId, @Nombre, @CantidadBase, @PrecioVenta, @PrecioCompra, @EsDefault, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_Actualizar
    @Id INT, @ProductoId INT, @Nombre NVARCHAR(50), @CantidadBase DECIMAL(18,4), @PrecioVenta DECIMAL(18,2),
    @PrecioCompra DECIMAL(18,2), @EsDefault BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.TipoPrecio
    SET Nombre = @Nombre, CantidadBase = @CantidadBase, PrecioVenta = @PrecioVenta,
        PrecioCompra = @PrecioCompra, EsDefault = @EsDefault
    WHERE Id = @Id AND ProductoId = @ProductoId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_Eliminar
    @ProductoId INT, @TipoPrecioId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM market.TipoPrecio WHERE Id = @TipoPrecioId AND ProductoId = @ProductoId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_LimpiarDefault
    @ProductoId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.TipoPrecio SET EsDefault = 0 WHERE ProductoId = @ProductoId AND EsDefault = 1;
END
GO

CREATE OR ALTER PROCEDURE market.usp_TipoPrecio_ActualizarPrecioCompra
    @TipoPrecioId INT, @PrecioCompra DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.TipoPrecio SET PrecioCompra = @PrecioCompra WHERE Id = @TipoPrecioId;
END
GO
