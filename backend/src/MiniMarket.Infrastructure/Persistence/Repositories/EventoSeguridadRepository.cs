using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class EventoSeguridadRepository : IEventoSeguridadRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EventoSeguridadRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task RegistrarAsync(EventoSeguridad evento)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_EventoSeguridad_Registrar", new
        {
            evento.EmpresaId, evento.ActorUsuarioId, evento.UsuarioObjetivoId, evento.Tipo, evento.Detalle,
            evento.Ip, evento.UserAgent, evento.FechaUtc, evento.Origen, evento.Metodo, evento.Ruta,
            evento.StatusCode, evento.DuracionMs, evento.Datos
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task RegistrarLoteAsync(IReadOnlyCollection<EventoSeguridad> eventos)
    {
        if (eventos.Count == 0) return;

        // market.EventoSeguridadListType es un table-valued parameter: SQL Server empareja sus columnas
        // por POSICIÓN, no por nombre, así que el orden de Columns.Add aquí debe calzar exactamente
        // con el CREATE TYPE (ver database/schema/market/02_types.sql).
        var tabla = new DataTable();
        tabla.Columns.Add("EmpresaId", typeof(int));
        tabla.Columns.Add("ActorUsuarioId", typeof(int));
        tabla.Columns.Add("UsuarioObjetivoId", typeof(int));
        tabla.Columns.Add("Tipo", typeof(string));
        tabla.Columns.Add("Detalle", typeof(string));
        tabla.Columns.Add("Ip", typeof(string));
        tabla.Columns.Add("UserAgent", typeof(string));
        tabla.Columns.Add("FechaUtc", typeof(DateTime));
        tabla.Columns.Add("Origen", typeof(string));
        tabla.Columns.Add("Metodo", typeof(string));
        tabla.Columns.Add("Ruta", typeof(string));
        tabla.Columns.Add("StatusCode", typeof(short));
        tabla.Columns.Add("DuracionMs", typeof(int));
        tabla.Columns.Add("Datos", typeof(string));

        foreach (var e in eventos)
        {
            tabla.Rows.Add(
                (object?)e.EmpresaId ?? DBNull.Value, (object?)e.ActorUsuarioId ?? DBNull.Value,
                (object?)e.UsuarioObjetivoId ?? DBNull.Value, e.Tipo, (object?)e.Detalle ?? DBNull.Value,
                (object?)e.Ip ?? DBNull.Value, (object?)e.UserAgent ?? DBNull.Value, e.FechaUtc, e.Origen,
                (object?)e.Metodo ?? DBNull.Value, (object?)e.Ruta ?? DBNull.Value,
                (object?)e.StatusCode ?? DBNull.Value, (object?)e.DuracionMs ?? DBNull.Value, (object?)e.Datos ?? DBNull.Value);
        }

        var parametros = new DynamicParameters();
        parametros.Add("@Eventos", tabla.AsTableValuedParameter("market.EventoSeguridadListType"));

        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_EventoSeguridad_RegistrarLote", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task<PaginaResultado<EventoSeguridadDto>> ListarAsync(int empresaId, AuditoriaFiltro filtro)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var parametros = new
        {
            empresaId,
            desde = filtro.Desde,
            hasta = filtro.Hasta,
            usuarioId = filtro.UsuarioId,
            tipo = filtro.Tipo,
            origen = filtro.Origen,
            offset = (filtro.Pagina - 1) * filtro.TamanoPagina,
            tamanoPagina = filtro.TamanoPagina
        };

        using var multi = await connection.QueryMultipleAsync(
            "market.usp_EventoSeguridad_Listar", parametros, commandType: CommandType.StoredProcedure);

        var total = await multi.ReadSingleAsync<int>();
        var filas = await multi.ReadAsync<EventoFila>();

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
