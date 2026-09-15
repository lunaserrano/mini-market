using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Enums;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class CajaService
{
    private readonly ICajaRepository _repository;
    private readonly ITenantContext _tenant;

    public CajaService(ICajaRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    private static CajaDto Mapear(Caja c) => new(
        c.Id, c.SucursalId, c.UsuarioAperturaId, c.FechaApertura, c.MontoInicial,
        c.UsuarioCierreId, c.FechaCierre, c.MontoFinalDeclarado, c.MontoFinalSistema, c.Diferencia, c.Estado);

    public async Task<CajaDto?> ObtenerActualAsync()
    {
        var caja = await _repository.ObtenerAbiertaPorUsuarioAsync(_tenant.EmpresaId, _tenant.UsuarioId);
        return caja is null ? null : Mapear(caja);
    }

    public async Task<IReadOnlyList<CajaDto>> ListarAsync(int? sucursalId) =>
        (await _repository.ListarAsync(_tenant.EmpresaId, sucursalId)).Select(Mapear).ToList();

    public async Task<CajaDto> AbrirAsync(AperturaCajaRequest request)
    {
        if (_tenant.SucursalId is null)
            throw new ReglaDeNegocioException("El usuario no tiene una sucursal activa asignada.");

        var existente = await _repository.ObtenerAbiertaPorUsuarioAsync(_tenant.EmpresaId, _tenant.UsuarioId);
        if (existente is not null)
            throw new CajaYaAbiertaException();

        var caja = new Caja
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = _tenant.SucursalId.Value,
            UsuarioAperturaId = _tenant.UsuarioId,
            FechaApertura = DateTime.UtcNow,
            MontoInicial = request.MontoInicial,
            Estado = "ABIERTA"
        };
        caja.Id = await _repository.AbrirAsync(caja);
        return Mapear(caja);
    }

    public async Task<CajaDto> CerrarAsync(int cajaId, CierreCajaRequest request)
    {
        var caja = await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, cajaId)
            ?? throw new EntidadNoEncontradaException("Caja", cajaId);

        if (caja.Estado != "ABIERTA")
            throw new CajaCerradaException("Esta caja ya fue cerrada.");

        var (ingresos, egresos) = await _repository.ObtenerTotalesMovimientosAsync(cajaId);
        var montoSistema = caja.MontoInicial + ingresos - egresos;

        caja.UsuarioCierreId = _tenant.UsuarioId;
        caja.FechaCierre = DateTime.UtcNow;
        caja.MontoFinalDeclarado = request.MontoFinalDeclarado;
        caja.MontoFinalSistema = montoSistema;
        caja.Diferencia = request.MontoFinalDeclarado - montoSistema;
        caja.Estado = "CERRADA";

        await _repository.CerrarAsync(caja);
        return Mapear(caja);
    }

    public async Task<int> RegistrarMovimientoAsync(int cajaId, MovimientoCajaCreateDto dto)
    {
        var tipo = Enum.TryParse<TipoMovimientoCaja>(dto.Tipo, ignoreCase: true, out var t)
            ? t
            : throw new ReglaDeNegocioException("Tipo de movimiento de caja inválido. Use INGRESO o EGRESO.");

        var caja = await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, cajaId)
            ?? throw new EntidadNoEncontradaException("Caja", cajaId);
        if (caja.Estado != "ABIERTA")
            throw new CajaCerradaException();

        return await _repository.RegistrarMovimientoAsync(new MovimientoCaja
        {
            CajaId = cajaId,
            Tipo = tipo == TipoMovimientoCaja.Ingreso ? "INGRESO" : "EGRESO",
            Concepto = dto.Concepto,
            Monto = dto.Monto,
            UsuarioId = _tenant.UsuarioId,
            Fecha = DateTime.UtcNow
        });
    }

    public async Task<IReadOnlyList<MovimientoCajaDto>> ListarMovimientosAsync(int cajaId) =>
        (await _repository.ListarMovimientosAsync(cajaId))
            .Select(m => new MovimientoCajaDto(m.Id, m.CajaId, m.Tipo, m.Concepto, m.Monto, m.Fecha))
            .ToList();
}
