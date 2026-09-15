using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class EmpresaService
{
    private readonly IEmpresaRepository _repository;
    private readonly ITenantContext _tenant;

    public EmpresaService(IEmpresaRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    private static EmpresaDto Mapear(Domain.Entities.Empresa e) =>
        new(e.Id, e.Nombre, e.RazonSocial, e.IdentificacionFiscal, e.ZonaHoraria, e.CodigoMoneda, e.SimboloMoneda, e.TasaImpuesto);

    public async Task<EmpresaDto> ObtenerActualAsync()
    {
        var empresa = await _repository.ObtenerPorIdAsync(_tenant.EmpresaId)
            ?? throw new EntidadNoEncontradaException("Empresa", _tenant.EmpresaId);
        return Mapear(empresa);
    }

    public async Task<EmpresaDto> ActualizarAsync(EmpresaUpdateDto dto)
    {
        var empresa = await _repository.ObtenerPorIdAsync(_tenant.EmpresaId)
            ?? throw new EntidadNoEncontradaException("Empresa", _tenant.EmpresaId);

        if (string.IsNullOrWhiteSpace(dto.CodigoMoneda) || string.IsNullOrWhiteSpace(dto.SimboloMoneda))
            throw new ReglaDeNegocioException("El código y el símbolo de moneda son obligatorios.");
        if (dto.TasaImpuesto < 0 || dto.TasaImpuesto > 100)
            throw new ReglaDeNegocioException("La tasa de impuesto debe estar entre 0 y 100.");

        empresa.Nombre = dto.Nombre;
        empresa.RazonSocial = dto.RazonSocial;
        empresa.IdentificacionFiscal = dto.IdentificacionFiscal;
        empresa.ZonaHoraria = dto.ZonaHoraria;
        empresa.CodigoMoneda = dto.CodigoMoneda.ToUpperInvariant();
        empresa.SimboloMoneda = dto.SimboloMoneda;
        empresa.TasaImpuesto = dto.TasaImpuesto;

        await _repository.ActualizarAsync(empresa);
        return Mapear(empresa);
    }
}
