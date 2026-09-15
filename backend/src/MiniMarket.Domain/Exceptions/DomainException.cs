namespace MiniMarket.Domain.Exceptions;

/// <summary>Excepción base para violaciones de reglas de negocio. La API la traduce a una respuesta 4xx
/// consistente en MiniMarket.Api.Middleware.ExceptionHandlingMiddleware.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class StockInsuficienteException : DomainException
{
    public StockInsuficienteException(string productoNombre, decimal stockActual, decimal cantidadSolicitada)
        : base($"Stock insuficiente para '{productoNombre}'. Disponible: {stockActual}, solicitado: {cantidadSolicitada}.")
    {
    }
}

public sealed class CajaCerradaException : DomainException
{
    public CajaCerradaException(string message = "No hay una caja abierta para esta operación.") : base(message) { }
}

public sealed class CajaYaAbiertaException : DomainException
{
    public CajaYaAbiertaException(string message = "Ya existe una caja abierta para este usuario/sucursal.") : base(message) { }
}

public sealed class PagosInsuficientesException : DomainException
{
    public PagosInsuficientesException(decimal total, decimal totalPagado)
        : base($"La suma de los pagos ({totalPagado:0.00}) no cubre el total de la venta ({total:0.00}).")
    {
    }
}

public sealed class EntidadNoEncontradaException : DomainException
{
    public EntidadNoEncontradaException(string entidad, object id)
        : base($"{entidad} con id '{id}' no fue encontrado(a).")
    {
    }
}

public sealed class ReglaDeNegocioException : DomainException
{
    public ReglaDeNegocioException(string message) : base(message) { }
}
