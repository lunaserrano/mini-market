using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class EventoSeguridadRepository : IEventoSeguridadRepository
{
    private const string InsertSql = """
        INSERT INTO EventoSeguridad (EmpresaId, ActorUsuarioId, UsuarioObjetivoId, Tipo, Detalle, Ip, UserAgent, FechaUtc,
                                     Origen, Metodo, Ruta, StatusCode, DuracionMs, Datos)
        VALUES (@EmpresaId, @ActorUsuarioId, @UsuarioObjetivoId, @Tipo, @Detalle, @Ip, @UserAgent, @FechaUtc,
                @Origen, @Metodo, @Ruta, @StatusCode, @DuracionMs, @Datos)
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public EventoSeguridadRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task RegistrarAsync(EventoSeguridad evento)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(InsertSql, evento);
    }

    public async Task RegistrarLoteAsync(IReadOnlyCollection<EventoSeguridad> eventos)
    {
        if (eventos.Count == 0) return;
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(InsertSql, eventos, transaction);
        transaction.Commit();
    }

    public async Task<PaginaResultado<EventoSeguridadDto>> ListarAsync(int empresaId, AuditoriaFiltro filtro)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        const string where = """
            WHERE e.EmpresaId = @empresaId
              AND (@desde IS NULL OR e.FechaUtc >= @desde)
              AND (@hasta IS NULL OR e.FechaUtc <= @hasta)
              AND (@usuarioId IS NULL OR e.ActorUsuarioId = @usuarioId OR e.UsuarioObjetivoId = @usuarioId)
              AND (@tipo IS NULL OR e.Tipo = @tipo)
              AND (@origen IS NULL OR e.Origen = @origen)
            """;

        var parametros = new
        {
            empresaId,
            desde = filtro.Desde,
            hasta = filtro.Hasta,
            usuarioId = filtro.UsuarioId,
            tipo = filtro.Tipo,
            origen = filtro.Origen,
            offset = (filtro.Pagina - 1) * filtro.TamanoPagina,
            tamano = filtro.TamanoPagina
        };

        var total = await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM EventoSeguridad e {where}", parametros);

        var filas = await connection.QueryAsync<EventoFila>($"""
            SELECT e.Id, e.FechaUtc, e.Tipo, e.Detalle, e.ActorUsuarioId, ua.NombreCompleto AS ActorNombre,
                   e.UsuarioObjetivoId, uo.NombreCompleto AS ObjetivoNombre, e.Ip,
                   e.Origen, e.Metodo, e.Ruta, e.StatusCode, e.DuracionMs, e.Datos
            FROM EventoSeguridad e
            LEFT JOIN Usuario ua ON ua.Id = e.ActorUsuarioId
            LEFT JOIN Usuario uo ON uo.Id = e.UsuarioObjetivoId
            {where}
            ORDER BY e.FechaUtc DESC, e.Id DESC
            OFFSET @offset ROWS FETCH NEXT @tamano ROWS ONLY
            """, parametros);

        // FechaUtc llega con Kind=Unspecified; se marca como UTC para que el JSON lleve "Z".
        var items = filas.Select(f => new EventoSeguridadDto(
            f.Id, DateTime.SpecifyKind(f.FechaUtc, DateTimeKind.Utc), f.Tipo, f.Detalle,
            f.ActorUsuarioId, f.ActorNombre, f.UsuarioObjetivoId, f.ObjetivoNombre, f.Ip,
            f.Origen, f.Metodo, f.Ruta, f.StatusCode, f.DuracionMs, f.Datos)).ToList();

        return new PaginaResultado<EventoSeguridadDto>(items, total, filtro.Pagina, filtro.TamanoPagina);
    }

    private sealed class EventoFila
    {
        public long Id { get; set; }
        public DateTime FechaUtc { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string? Detalle { get; set; }
        public int? ActorUsuarioId { get; set; }
        public string? ActorNombre { get; set; }
        public int? UsuarioObjetivoId { get; set; }
        public string? ObjetivoNombre { get; set; }
        public string? Ip { get; set; }
        public string Origen { get; set; } = string.Empty;
        public string? Metodo { get; set; }
        public string? Ruta { get; set; }
        public short? StatusCode { get; set; }
        public int? DuracionMs { get; set; }
        public string? Datos { get; set; }
    }
}
