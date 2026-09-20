using System.Threading.Channels;
using MiniMarket.Application.Interfaces;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Services;

/// <summary>
/// Cola en memoria entre quien produce eventos de auditoría (middleware, endpoint del navegador) y el
/// escritor en segundo plano que los persiste por lotes. Así registrar cada petición no suma una ida y
/// vuelta a la base de datos a la respuesta del usuario.
/// </summary>
public class AuditoriaCola : IAuditoriaCola
{
    private readonly Channel<EventoSeguridad> _canal;

    public AuditoriaCola(int capacidad = 20_000)
    {
        _canal = Channel.CreateBounded<EventoSeguridad>(new BoundedChannelOptions(capacidad)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public ChannelReader<EventoSeguridad> Lector => _canal.Reader;

    /// <summary>Devuelve false si la cola está llena (el evento se pierde: la BD no da abasto).</summary>
    public bool Encolar(EventoSeguridad evento) => _canal.Writer.TryWrite(evento);

    /// <summary>Cierra la cola para nuevas entradas; el lector termina de vaciar lo pendiente. Se usa al apagar la aplicación.</summary>
    public void Completar() => _canal.Writer.TryComplete();
}
