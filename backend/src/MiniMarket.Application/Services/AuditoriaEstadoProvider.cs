using Microsoft.Extensions.Caching.Memory;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;

namespace MiniMarket.Application.Services;

/// <summary>
/// Combina el interruptor global de appsettings (apagado de emergencia para todas las empresas, sin
/// depender de que la BD responda) con el override por empresa/sucursal de market.Parametro.
/// Cachea la consulta a BD ~30s: apagar/prender la auditoría de una empresa tarda hasta ese margen
/// en reflejarse, a cambio de no sumar una consulta a BD en cada petición auditada.
/// </summary>
public class AuditoriaEstadoProvider : IAuditoriaEstadoProvider
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromSeconds(30);

    private readonly IParametroRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly AuditoriaOptions _opciones;

    public AuditoriaEstadoProvider(IParametroRepository repository, IMemoryCache cache, AuditoriaOptions opciones)
    {
        _repository = repository;
        _cache = cache;
        _opciones = opciones;
    }

    public async Task<bool> EstaHabilitadaAsync(int? empresaId, int? sucursalId)
    {
        // Sin empresa resuelta (ej. login fallido con usuario inexistente) siempre se registra: es
        // justo el caso de seguridad que no se puede permitir perder.
        if (!_opciones.Habilitada || empresaId is null) return _opciones.Habilitada;

        var clave = $"auditoria-habilitada:{empresaId}:{sucursalId ?? 0}";
        if (_cache.TryGetValue(clave, out bool habilitada)) return habilitada;

        habilitada = await _repository.ObtenerAuditoriaHabilitadaAsync(empresaId.Value, sucursalId) ?? true;
        _cache.Set(clave, habilitada, DuracionCache);
        return habilitada;
    }
}
