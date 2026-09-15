using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IProductoRepository
{
    Task<IReadOnlyList<ProductoDto>> ListarAsync(int empresaId);
    Task<ProductoDto?> ObtenerPorIdAsync(int empresaId, int id);
    Task<Producto?> ObtenerEntidadAsync(int empresaId, int id);

    /// <summary>Búsqueda rápida para el POS por nombre o código de barras, con stock de la sucursal activa.</summary>
    Task<IReadOnlyList<ProductoPosDto>> BuscarParaPosAsync(int empresaId, int sucursalId, string termino);

    Task<int> CrearAsync(Producto producto);
    Task ActualizarAsync(Producto producto);
    Task CambiarEstadoAsync(int empresaId, int id, string estado);

    Task<IReadOnlyList<TipoPrecio>> ListarTiposPrecioAsync(int productoId);
    Task<TipoPrecio?> ObtenerTipoPrecioAsync(int productoId, int tipoPrecioId);
    Task<int> CrearTipoPrecioAsync(TipoPrecio tipoPrecio, IDbTransaction? transaction = null);
    Task ActualizarTipoPrecioAsync(TipoPrecio tipoPrecio);
    Task EliminarTipoPrecioAsync(int productoId, int tipoPrecioId);
    /// <summary>Desmarca EsDefault=1 de cualquier otro tipo de precio del producto (mantiene el índice único filtrado consistente).</summary>
    Task LimpiarDefaultAsync(int productoId, IDbTransaction? transaction = null);
}
