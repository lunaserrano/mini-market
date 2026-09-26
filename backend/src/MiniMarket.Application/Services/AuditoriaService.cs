using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;

namespace MiniMarket.Application.Services;

public class AuditoriaService
{
    private const int TamanoMaximo = 100;
    public const int MaxEventosPorLote = 100;
    /// <summary>Un evento del navegador más antiguo que esto se considera con reloj desajustado y se fecha con la hora del servidor.</summary>
    private static readonly TimeSpan AntiguedadMaximaCliente = TimeSpan.FromMinutes(30);

    private readonly IEventoSeguridadRepository _repository;
    private readonly IParametroRepository _parametros;
    private readonly ITenantContext _tenant;
    private readonly IAuditoriaCola _cola;
    private readonly IRequestInfo _request;
    private readonly TimeProvider _time;
    private readonly IAuditoriaEstadoProvider _estado;

    public AuditoriaService(
        IEventoSeguridadRepository repository, IParametroRepository parametros, ITenantContext tenant, IAuditoriaCola cola,
        IRequestInfo request, TimeProvider time, IAuditoriaEstadoProvider estado)
    {
        _repository = repository;
        _parametros = parametros;
        _tenant = tenant;
        _cola = cola;
        _request = request;
        _time = time;
        _estado = estado;
    }

    /// <summary>Bandera de auditoría de la empresa actual (SucursalId = 0: valor de toda la empresa).</summary>
    public async Task<AuditoriaEstadoDto> ObtenerEstadoAsync() =>
        new(await _parametros.ObtenerAuditoriaHabilitadaAsync(_tenant.EmpresaId, sucursalId: 0) ?? true);

    public Task ActualizarEstadoAsync(AuditoriaEstadoDto dto) =>
        _parametros.ActualizarAuditoriaHabilitadaAsync(_tenant.EmpresaId, sucursalId: 0, dto.Habilitada, _tenant.UsuarioId);

    public Task<PaginaResultado<EventoSeguridadDto>> ListarAsync(AuditoriaFiltro filtro)
    {
        var normalizado = filtro with
        {
            Pagina = Math.Max(1, filtro.Pagina),
            TamanoPagina = Math.Clamp(filtro.TamanoPagina, 1, TamanoMaximo),
            Tipo = string.IsNullOrWhiteSpace(filtro.Tipo) ? null : filtro.Tipo.Trim(),
            Origen = string.IsNullOrWhiteSpace(filtro.Origen) ? null : filtro.Origen.Trim().ToUpperInvariant()
        };
        return _repository.ListarAsync(_tenant.EmpresaId, normalizado);
    }

    /// <summary>
    /// Registra las acciones que el navegador reporta (clics, navegación). La empresa y el usuario salen SIEMPRE
    /// del token, nunca del lote, y solo se aceptan tipos de UI: el cliente no puede fabricar eventos de seguridad.
    /// </summary>
    public async Task RegistrarEventosCliente(IReadOnlyList<EventoClienteDto> eventos)
    {
        if (!await _estado.EstaHabilitadaAsync(_tenant.EmpresaId, _tenant.SucursalId)) return;

        if (eventos.Count > MaxEventosPorLote)
            throw new ReglaDeNegocioException($"Un lote admite como máximo {MaxEventosPorLote} eventos.");

        // Se valida todo el lote antes de encolar nada: un lote inválido no debe registrarse a medias.
        foreach (var e in eventos)
        {
            if (!TipoEventoSeguridad.PermitidosDesdeCliente.Contains(e.Tipo))
                throw new ReglaDeNegocioException($"Tipo de evento no permitido: {AuditoriaDatos.Truncar(e.Tipo, 40)}.");
        }

        var ahora = _time.GetUtcNow().UtcDateTime;
        foreach (var e in eventos)
        {
            var fecha = e.FechaUtc is { } f && f <= ahora && ahora - f <= AntiguedadMaximaCliente ? f : ahora;

            _cola.Encolar(new EventoSeguridad
            {
                EmpresaId = _tenant.EmpresaId,
                ActorUsuarioId = _tenant.UsuarioId,
                Tipo = e.Tipo,
                Origen = OrigenEvento.Ui,
                Ruta = AuditoriaDatos.Truncar(e.Ruta, 300),
                Detalle = AuditoriaDatos.Truncar(e.Detalle, 500),
                Datos = AuditoriaDatos.SerializarDescriptor(e.Datos),
                Ip = AuditoriaDatos.Truncar(_request.Ip, 45),
                UserAgent = AuditoriaDatos.Truncar(_request.UserAgent, 250),
                FechaUtc = fecha
            });
        }
    }
}
