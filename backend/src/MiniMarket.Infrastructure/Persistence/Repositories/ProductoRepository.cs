using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class ProductoRepository : IProductoRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private static TipoPrecioDto MapTipoPrecio(TipoPrecio t) =>
        new(t.Id, t.Nombre, t.CantidadBase, t.PrecioVenta, t.PrecioCompra, t.EsDefault, t.Estado);

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var productos = (await connection.QueryAsync<Producto>(
            "SELECT * FROM Producto WHERE EmpresaId = @empresaId ORDER BY Nombre", new { empresaId })).ToList();
        if (productos.Count == 0) return Array.Empty<ProductoDto>();

        var categorias = (await connection.QueryAsync<(int Id, string Nombre)>(
            "SELECT Id, Nombre FROM Categoria WHERE Id IN @ids",
            new { ids = productos.Select(p => p.CategoriaId).Distinct() })).ToDictionary(c => c.Id, c => c.Nombre);

        var tiposPrecio = (await connection.QueryAsync<TipoPrecio>(
            "SELECT * FROM TipoPrecio WHERE ProductoId IN @ids ORDER BY Nombre",
            new { ids = productos.Select(p => p.Id) })).ToLookup(t => t.ProductoId);

        return productos.Select(p => new ProductoDto(
            p.Id, p.CategoriaId, categorias.GetValueOrDefault(p.CategoriaId), p.ProveedorId, p.Nombre, p.Descripcion,
            p.CodigoBarras, p.CodigoInterno, p.ImagenPath, p.UnidadBase, p.Estado,
            tiposPrecio[p.Id].Select(MapTipoPrecio).ToList()
        )).ToList();
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var producto = await connection.QuerySingleOrDefaultAsync<Producto>(
            "SELECT * FROM Producto WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
        if (producto is null) return null;

        var categoriaNombre = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Nombre FROM Categoria WHERE Id = @categoriaId", new { producto.CategoriaId });

        var tiposPrecio = await connection.QueryAsync<TipoPrecio>(
            "SELECT * FROM TipoPrecio WHERE ProductoId = @id ORDER BY Nombre", new { id });

        return new ProductoDto(
            producto.Id, producto.CategoriaId, categoriaNombre, producto.ProveedorId, producto.Nombre, producto.Descripcion,
            producto.CodigoBarras, producto.CodigoInterno, producto.ImagenPath, producto.UnidadBase, producto.Estado,
            tiposPrecio.Select(MapTipoPrecio).ToList());
    }

    public async Task<Producto?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Producto>(
            "SELECT * FROM Producto WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<IReadOnlyList<ProductoPosDto>> BuscarParaPosAsync(int empresaId, int sucursalId, string termino)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        const string sql = """
            SELECT TOP 20 p.Id, p.Nombre, p.CodigoBarras, p.UnidadBase,
                   ISNULL(i.StockActual, 0) AS StockActual
            FROM Producto p
            LEFT JOIN Inventario i ON i.ProductoId = p.Id AND i.SucursalId = @sucursalId
            WHERE p.EmpresaId = @empresaId AND p.Estado = 'A'
              AND (p.CodigoBarras = @termino OR p.Nombre LIKE '%' + @termino + '%')
            ORDER BY p.Nombre
            """;

        var filas = (await connection.QueryAsync<(int Id, string Nombre, string? CodigoBarras, string UnidadBase, decimal StockActual)>(
            sql, new { empresaId, sucursalId, termino })).ToList();
        if (filas.Count == 0) return Array.Empty<ProductoPosDto>();

        var tiposPrecio = (await connection.QueryAsync<TipoPrecio>(
            "SELECT * FROM TipoPrecio WHERE ProductoId IN @ids AND Estado = 'A' ORDER BY Nombre",
            new { ids = filas.Select(f => f.Id) })).ToLookup(t => t.ProductoId);

        return filas.Select(f => new ProductoPosDto(
            f.Id, f.Nombre, f.CodigoBarras, f.UnidadBase, f.StockActual,
            tiposPrecio[f.Id].Select(MapTipoPrecio).ToList()
        )).ToList();
    }

    public async Task<int> CrearAsync(Producto producto)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Producto (EmpresaId, CategoriaId, ProveedorId, Nombre, Descripcion, CodigoBarras, CodigoInterno,
                ImagenPath, UnidadBase, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @CategoriaId, @ProveedorId, @Nombre, @Descripcion, @CodigoBarras, @CodigoInterno,
                @ImagenPath, @UnidadBase, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """;
        return await connection.QuerySingleAsync<int>(sql, producto);
    }

    public async Task ActualizarAsync(Producto producto)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Producto SET CategoriaId = @CategoriaId, ProveedorId = @ProveedorId, Nombre = @Nombre,
                Descripcion = @Descripcion, CodigoBarras = @CodigoBarras, CodigoInterno = @CodigoInterno,
                ImagenPath = @ImagenPath, UnidadBase = @UnidadBase,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """;
        await connection.ExecuteAsync(sql, producto);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("UPDATE Producto SET Estado = @estado WHERE Id = @id AND EmpresaId = @empresaId", new { empresaId, id, estado });
    }

    public async Task<IReadOnlyList<TipoPrecio>> ListarTiposPrecioAsync(int productoId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<TipoPrecio>(
            "SELECT * FROM TipoPrecio WHERE ProductoId = @productoId ORDER BY Nombre", new { productoId });
        return items.AsList();
    }

    public async Task<TipoPrecio?> ObtenerTipoPrecioAsync(int productoId, int tipoPrecioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<TipoPrecio>(
            "SELECT * FROM TipoPrecio WHERE ProductoId = @productoId AND Id = @tipoPrecioId", new { productoId, tipoPrecioId });
    }

    public async Task<int> CrearTipoPrecioAsync(TipoPrecio tipoPrecio, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            const string sql = """
                INSERT INTO TipoPrecio (ProductoId, Nombre, CantidadBase, PrecioVenta, PrecioCompra, EsDefault, Estado)
                OUTPUT INSERTED.Id
                VALUES (@ProductoId, @Nombre, @CantidadBase, @PrecioVenta, @PrecioCompra, @EsDefault, @Estado)
                """;
            return await connection.QuerySingleAsync<int>(sql, tipoPrecio, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarTipoPrecioAsync(TipoPrecio tipoPrecio)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE TipoPrecio SET Nombre = @Nombre, CantidadBase = @CantidadBase, PrecioVenta = @PrecioVenta,
                PrecioCompra = @PrecioCompra, EsDefault = @EsDefault
            WHERE Id = @Id AND ProductoId = @ProductoId
            """;
        await connection.ExecuteAsync(sql, tipoPrecio);
    }

    public async Task EliminarTipoPrecioAsync(int productoId, int tipoPrecioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("DELETE FROM TipoPrecio WHERE Id = @tipoPrecioId AND ProductoId = @productoId", new { productoId, tipoPrecioId });
    }

    public async Task LimpiarDefaultAsync(int productoId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            await connection.ExecuteAsync(
                "UPDATE TipoPrecio SET EsDefault = 0 WHERE ProductoId = @productoId AND EsDefault = 1",
                new { productoId }, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarPrecioCompraAsync(int tipoPrecioId, decimal precioCompra, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "UPDATE TipoPrecio SET PrecioCompra = @precioCompra WHERE Id = @tipoPrecioId",
            new { tipoPrecioId, precioCompra }, transaction);
    }
}
