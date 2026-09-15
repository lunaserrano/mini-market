using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IInventarioRepository
{
    Task<IReadOnlyList<InventarioDto>> ListarAsync(int empresaId, int? sucursalId, int? productoId);
    Task<Inventario?> ObtenerAsync(int productoId, int sucursalId, IDbTransaction? transaction = null);

    /// <summary>Bloquea la fila (UPDLOCK,ROWLOCK) dentro de una transacción — usado por VentaService para
    /// evitar condiciones de carrera al descontar stock concurrentemente.</summary>
    Task<Inventario?> ObtenerParaActualizarAsync(int productoId, int sucursalId, IDbTransaction transaction);

    Task<int> CrearAsync(Inventario inventario, IDbTransaction? transaction = null);
    Task ActualizarStockAsync(int productoId, int sucursalId, decimal nuevoStock, IDbTransaction transaction);

    Task<int> RegistrarMovimientoAsync(MovimientoInventario movimiento, IDbTransaction transaction);
    Task<IReadOnlyList<MovimientoInventarioDto>> ListarMovimientosAsync(int empresaId, MovimientoInventarioFiltro filtro);
}
