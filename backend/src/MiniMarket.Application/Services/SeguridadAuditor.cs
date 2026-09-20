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

    public SeguridadAuditor(IEventoSeguridadRepository repository, IRequestInfo request, TimeProvider time, ILogger<SeguridadAuditor> logger)
    {
        _repository = repository;
        _request = request;
        _time = time;
        _logger = logger;
    }

    public async Task RegistrarAsync(string tipo, int? empresaId, int? actorUsuarioId, int? usuarioObjetivoId, string? detalle = null)
    {
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
