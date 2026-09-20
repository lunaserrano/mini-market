using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Enums;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

/// <summary>
/// Cobranza de ventas a crédito. El crédito nace en <see cref="VentaService.CrearAsync"/> (dentro de
/// la transacción de la venta); aquí se consulta y se registran los abonos hasta saldarlo.
/// </summary>
public class CreditoService
{
    public static readonly IReadOnlyList<string> MetodosAbono = new[] { "EFECTIVO", "TARJETA", "TRANSFERENCIA" };
    private static readonly string[] Estados = { Credito.Pendiente, Credito.Pagado, Credito.Anulado };

    private readonly ICreditoRepository _creditoRepository;
    private readonly ICajaRepository _cajaRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenant;

    public CreditoService(
        ICreditoRepository creditoRepository,
        ICajaRepository cajaRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenant)
    {
        _creditoRepository = creditoRepository;
        _cajaRepository = cajaRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<CreditoResumenDto>> ListarAsync(int? clienteId, string? estado)
    {
        var estadoNormalizado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (estadoNormalizado is not null && !Estados.Contains(estadoNormalizado))
            throw new ReglaDeNegocioException("Estado de crédito inválido. Use PENDIENTE, PAGADO o ANULADO.");

        return _creditoRepository.ListarAsync(_tenant.EmpresaId, clienteId, estadoNormalizado);
    }

    public async Task<CreditoDto> ObtenerAsync(int id) =>
        await _creditoRepository.ObtenerDetalleAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Crédito", id);

    /// <summary>Registra un abono y devuelve el crédito actualizado. Si el saldo llega a cero, el crédito queda PAGADO.</summary>
    public async Task<CreditoDto> AbonarAsync(int id, AbonoCreditoCreateDto request)
    {
        var metodo = request.Metodo.Trim().ToUpperInvariant();
        if (!MetodosAbono.Contains(metodo))
            throw new ReglaDeNegocioException("Método de pago inválido. Use EFECTIVO, TARJETA o TRANSFERENCIA.");

        var monto = Math.Round(request.Monto, 2, MidpointRounding.AwayFromZero);
        if (monto <= 0)
            throw new ReglaDeNegocioException("El abono debe ser mayor a cero.");

        // El efectivo entra a la caja del usuario, así que necesita una abierta (igual que vender en efectivo).
        Caja? caja = null;
        if (metodo == "EFECTIVO")
        {
            caja = await _cajaRepository.ObtenerAbiertaPorUsuarioAsync(_tenant.EmpresaId, _tenant.UsuarioId)
                ?? throw new CajaCerradaException("Debe abrir una caja antes de recibir abonos en efectivo.");
        }

        using var uow = _unitOfWorkFactory.Create();

        var credito = await _creditoRepository.ObtenerParaActualizarAsync(_tenant.EmpresaId, id, uow.Transaction)
            ?? throw new EntidadNoEncontradaException("Crédito", id);

        if (credito.Estado == Credito.Pagado)
            throw new ReglaDeNegocioException("Este crédito ya está pagado por completo.");
        if (credito.Estado == Credito.Anulado)
            throw new ReglaDeNegocioException("Este crédito está anulado: la venta que lo originó fue anulada.");
        if (monto > credito.SaldoPendiente)
            throw new ReglaDeNegocioException($"El abono ({monto:0.00}) supera el saldo pendiente ({credito.SaldoPendiente:0.00}).");

        var ahora = DateTime.UtcNow;
        await _creditoRepository.RegistrarAbonoAsync(new AbonoCredito
        {
            CreditoId = credito.Id,
            CajaId = caja?.Id,
            UsuarioId = _tenant.UsuarioId,
            Metodo = metodo,
            Monto = monto,
            Referencia = string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim(),
            Fecha = ahora
        }, uow.Transaction);

        var nuevoSaldo = credito.SaldoPendiente - monto;
        var saldado = nuevoSaldo == 0;
        await _creditoRepository.ActualizarSaldoAsync(credito.Id, nuevoSaldo,
            saldado ? Credito.Pagado : Credito.Pendiente, saldado ? ahora : null, uow.Transaction);

        if (caja is not null)
        {
            await _cajaRepository.RegistrarMovimientoAsync(new MovimientoCaja
            {
                CajaId = caja.Id,
                Tipo = "INGRESO",
                Concepto = $"Abono a crédito #{credito.Id}",
                Monto = monto,
                UsuarioId = _tenant.UsuarioId,
                Fecha = ahora,
                DocumentoReferenciaTipo = DocumentoOrigenTipo.AbonoCredito.ToString(),
                DocumentoReferenciaId = credito.Id
            }, uow.Transaction);
        }

        uow.Commit();

        return await ObtenerAsync(id);
    }
}
