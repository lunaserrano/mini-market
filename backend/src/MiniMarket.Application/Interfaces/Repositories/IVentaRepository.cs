using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IVentaRepository
{
    /// <summary>Siguiente folio correlativo para la sucursal (calculado dentro de la misma transacción para evitar duplicados).</summary>
    Task<int> ObtenerSiguienteFolioAsync(int sucursalId, IDbTransaction transaction);
    Task<int> CrearAsync(Venta venta, IDbTransaction transaction);
    Task CrearDetalleAsync(DetalleVenta detalle, IDbTransaction transaction);
    Task CrearPagoAsync(PagoVenta pago, IDbTransaction transaction);

    Task<Venta?> ObtenerEntidadAsync(int empresaId, int id);
    Task<VentaDto?> ObtenerDetalleAsync(int empresaId, int id);
    Task<IReadOnlyList<VentaResumenDto>> ListarAsync(int empresaId, int? sucursalId, int? usuarioId, DateTime? desde, DateTime? hasta);
    Task AnularAsync(int id, int usuarioAnulacionId, string motivo, IDbTransaction transaction);
}
