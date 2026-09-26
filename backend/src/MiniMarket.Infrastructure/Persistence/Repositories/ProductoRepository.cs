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
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Producto_Listar", new { empresaId }, commandType: CommandType.StoredProcedure);

        var productos = (await multi.ReadAsync<Producto>()).ToList();
        var categorias = (await multi.ReadAsync<(int Id, string Nombre)>()).ToDictionary(c => c.Id, c => c.Nombre);
        var tiposPrecio = (await multi.ReadAsync<TipoPrecio>()).ToLookup(t => t.ProductoId);

        return productos.Select(p => new ProductoDto(
            p.Id, p.CategoriaId, categorias.GetValueOrDefault(p.CategoriaId), p.ProveedorId, p.Nombre, p.Descripcion,
            p.CodigoBarras, p.CodigoInterno, p.ImagenPath, p.UnidadBase, p.Estado,
            tiposPrecio[p.Id].Select(MapTipoPrecio).ToList()
        )).ToList();
    }

    public async Task<ProductoDto?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Producto_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);

        var producto = await multi.ReadSingleOrDefaultAsync<ProductoConCategoria>();
        var tiposPrecio = await multi.ReadAsync<TipoPrecio>();
        if (producto is null) return null;

        return new ProductoDto(
            producto.Id, producto.CategoriaId, producto.CategoriaNombre, producto.ProveedorId, producto.Nombre, producto.Descripcion,
            producto.CodigoBarras, producto.CodigoInterno, producto.ImagenPath, producto.UnidadBase, producto.Estado,
            tiposPrecio.Select(MapTipoPrecio).ToList());
    }

    public async Task<Producto?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Producto>(
            "market.usp_Producto_ObtenerEntidad", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<ProductoPosDto>> BuscarParaPosAsync(int empresaId, int sucursalId, string termino)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Producto_BuscarParaPos", new { empresaId, sucursalId, termino }, commandType: CommandType.StoredProcedure);

        var filas = (await multi.ReadAsync<(int Id, string Nombre, string? CodigoBarras, string UnidadBase, decimal StockActual)>()).ToList();
        var tiposPrecio = (await multi.ReadAsync<TipoPrecio>()).ToLookup(t => t.ProductoId);

        return filas.Select(f => new ProductoPosDto(
            f.Id, f.Nombre, f.CodigoBarras, f.UnidadBase, f.StockActual,
            tiposPrecio[f.Id].Select(MapTipoPrecio).ToList()
        )).ToList();
    }

    public async Task<int> CrearAsync(Producto producto)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Producto_Crear", new
        {
            producto.EmpresaId, producto.CategoriaId, producto.ProveedorId, producto.Nombre, producto.Descripcion,
            producto.CodigoBarras, producto.CodigoInterno, producto.ImagenPath, producto.UnidadBase, producto.Estado,
            producto.CreadoPorUsuarioId, producto.FechaCreacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Producto producto)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Producto_Actualizar", new
        {
            producto.Id, producto.EmpresaId, producto.CategoriaId, producto.ProveedorId, producto.Nombre,
            producto.Descripcion, producto.CodigoBarras, producto.CodigoInterno, producto.ImagenPath, producto.UnidadBase,
            producto.ModificadoPorUsuarioId, producto.FechaModificacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Producto_CambiarEstado", new { empresaId, id, estado }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<TipoPrecio>> ListarTiposPrecioAsync(int productoId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<TipoPrecio>(
            "market.usp_TipoPrecio_Listar", new { productoId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<TipoPrecio?> ObtenerTipoPrecioAsync(int productoId, int tipoPrecioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<TipoPrecio>(
            "market.usp_TipoPrecio_ObtenerPorId", new { productoId, tipoPrecioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearTipoPrecioAsync(TipoPrecio tipoPrecio, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleAsync<int>("market.usp_TipoPrecio_Crear", new
            {
                tipoPrecio.ProductoId, tipoPrecio.Nombre, tipoPrecio.CantidadBase, tipoPrecio.PrecioVenta,
                tipoPrecio.PrecioCompra, tipoPrecio.EsDefault, tipoPrecio.Estado
            }, transaction, commandType: CommandType.StoredProcedure);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarTipoPrecioAsync(TipoPrecio tipoPrecio)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_TipoPrecio_Actualizar", new
        {
            tipoPrecio.Id, tipoPrecio.ProductoId, tipoPrecio.Nombre, tipoPrecio.CantidadBase,
            tipoPrecio.PrecioVenta, tipoPrecio.PrecioCompra, tipoPrecio.EsDefault
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task EliminarTipoPrecioAsync(int productoId, int tipoPrecioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_TipoPrecio_Eliminar", new { productoId, tipoPrecioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task LimpiarDefaultAsync(int productoId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            await connection.ExecuteAsync(
                "market.usp_TipoPrecio_LimpiarDefault", new { productoId }, transaction, commandType: CommandType.StoredProcedure);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarPrecioCompraAsync(int tipoPrecioId, decimal precioCompra, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "market.usp_TipoPrecio_ActualizarPrecioCompra", new { tipoPrecioId, precioCompra }, transaction,
            commandType: CommandType.StoredProcedure);
    }

    private sealed record ProductoConCategoria(
        int Id, int EmpresaId, int CategoriaId, int? ProveedorId, string Nombre, string? Descripcion,
        string? CodigoBarras, string? CodigoInterno, string? ImagenPath, string UnidadBase, string Estado,
        string? CategoriaNombre);
}
