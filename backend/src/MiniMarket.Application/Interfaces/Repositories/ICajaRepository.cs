using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface ICajaRepository
{
    Task<Caja?> ObtenerAbiertaPorUsuarioAsync(int empresaId, int usuarioId);
    Task<Caja?> ObtenerPorIdAsync(int empresaId, int id);
    /// <summary>Historial de cajas (abiertas y cerradas) con nombres de usuario resueltos, para listados/reportes.</summary>
    Task<IReadOnlyList<CajaDto>> ListarAsync(int empresaId, int? sucursalId);
    Task<int> AbrirAsync(Caja caja);
    Task CerrarAsync(Caja caja);

    Task<int> RegistrarMovimientoAsync(MovimientoCaja movimiento, IDbTransaction? transaction = null);
    Task<IReadOnlyList<MovimientoCaja>> ListarMovimientosAsync(int cajaId);
    /// <summary>Suma de ingresos/egresos manuales + ventas en efectivo ligadas a la caja, usado al calcular el cierre.</summary>
    Task<(decimal Ingresos, decimal Egresos)> ObtenerTotalesMovimientosAsync(int cajaId, IDbTransaction? transaction = null);
}
