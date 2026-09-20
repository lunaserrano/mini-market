using System.Data;
using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface ICreditoRepository
{
    Task<int> CrearAsync(Credito credito, IDbTransaction transaction);

    /// <summary>Lee el crédito bloqueándolo (UPDLOCK) hasta el fin de la transacción: dos abonos simultáneos no pueden pisarse el saldo.</summary>
    Task<Credito?> ObtenerParaActualizarAsync(int empresaId, int id, IDbTransaction transaction);
    /// <summary>Crédito ligado a una venta (o null si se vendió al contado), dentro de la transacción de anulación.</summary>
    Task<Credito?> ObtenerPorVentaAsync(int ventaId, IDbTransaction transaction);
    Task<int> ContarAbonosAsync(int creditoId, IDbTransaction transaction);

    Task RegistrarAbonoAsync(AbonoCredito abono, IDbTransaction transaction);
    Task ActualizarSaldoAsync(int id, decimal saldoPendiente, string estado, DateTime? fechaCancelacion, IDbTransaction transaction);
    Task AnularAsync(int id, IDbTransaction transaction);

    /// <param name="estado">"PENDIENTE" | "PAGADO" | "ANULADO"; null trae todos.</param>
    Task<IReadOnlyList<CreditoResumenDto>> ListarAsync(int empresaId, int? clienteId, string? estado);
    Task<CreditoDto?> ObtenerDetalleAsync(int empresaId, int id);
}
