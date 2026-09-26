using Microsoft.Extensions.Logging;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Services;

public class SeguridadAuditor : ISeguridadAuditor
{
    private readonly IEventoSeguridadRepository _repository;
    private readonly IRequestInfo _request;
    private readonly TimeProvider _time;
    private readonly ILogger<SeguridadAuditor> _logger;
    private readonly IAuditoriaEstadoProvider _estado;

    public SeguridadAuditor(IEventoSeguridadRepository repository, IRequestInfo request, TimeProvider time, ILogger<SeguridadAuditor> logger, IAuditoriaEstadoProvider estado)
    {
        _repository = repository;
        _request = request;
        _time = time;
        _logger = logger;
        _estado = estado;
    }

    public async Task RegistrarAsync(string tipo, int? empresaId, int? actorUsuarioId, int? usuarioObjetivoId, string? detalle = null)
    {
        // Sin SucursalId propio (login/roles/usuarios no lo rastrean): se evalúa a nivel de empresa.
        if (!await _estado.EstaHabilitadaAsync(empresaId, sucursalId: null)) return;

        try
        {
            await _repository.RegistrarAsync(new EventoSeguridad
            {
                EmpresaId = empresaId,
                ActorUsuarioId = actorUsuarioId,
                UsuarioObjetivoId = usuarioObjetivoId,
                Tipo = tipo,
                Detalle = Truncar(detalle, 500),
                Ip = Truncar(_request.Ip, 45),
                UserAgent = Truncar(_request.UserAgent, 250),
                FechaUtc = _time.GetUtcNow().UtcDateTime
            });
        }
        catch (Exception ex)
        {
            // La auditoría es best-effort: nunca debe impedir login, cambio de rol, etc.
            _logger.LogError(ex, "No se pudo registrar el evento de seguridad {Tipo}", tipo);
        }
    }

    private static string? Truncar(string? valor, int max) =>
        valor is null || valor.Length <= max ? valor : valor[..max];
}
