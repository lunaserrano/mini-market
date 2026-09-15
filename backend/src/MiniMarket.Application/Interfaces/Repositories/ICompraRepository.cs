using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface ICompraRepository
{
    Task<int> CrearAsync(Compra compra, IDbTransaction transaction);
    Task CrearDetalleAsync(DetalleCompra detalle, IDbTransaction transaction);

    Task<Compra?> ObtenerEntidadAsync(int empresaId, int id);
    Task<CompraDto?> ObtenerDetalleAsync(int empresaId, int id);
    Task<IReadOnlyList<CompraResumenDto>> ListarAsync(int empresaId, int? sucursalId);
    Task AnularAsync(int id, IDbTransaction transaction);
}
