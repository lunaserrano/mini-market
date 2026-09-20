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

/// <summary>Credenciales incorrectas (mensaje genérico a propósito: no revela si falló el usuario o la contraseña). HTTP 401.</summary>
public sealed class CredencialesInvalidasException : DomainException
{
    public CredencialesInvalidasException(string message = "Usuario o contraseña incorrectos.") : base(message) { }
}

/// <summary>Cuenta bloqueada temporalmente por intentos fallidos. HTTP 423.</summary>
public sealed class CuentaBloqueadaException : DomainException
{
    public CuentaBloqueadaException(DateTime bloqueadoHastaUtc)
        : base($"La cuenta está bloqueada temporalmente por intentos fallidos. Intente de nuevo en {Math.Max(1, (int)Math.Ceiling((bloqueadoHastaUtc - DateTime.UtcNow).TotalMinutes))} minuto(s) o solicite el desbloqueo a un administrador.")
    {
    }
}

/// <summary>Falta el token o le faltan claims requeridos. HTTP 401.</summary>
public sealed class NoAutenticadoException : DomainException
{
    public NoAutenticadoException(string message = "No autenticado.") : base(message) { }
}

/// <summary>Autenticado pero sin el permiso requerido para la operación. HTTP 403.</summary>
public sealed class PermisoDenegadoException : DomainException
{
    public PermisoDenegadoException(string message = "No tiene permiso para realizar esta operación.") : base(message) { }
}

/// <summary>Errores de validación de entrada, por campo. HTTP 400.</summary>
public sealed class ValidacionException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    public ValidacionException(IReadOnlyDictionary<string, string[]> errores)
        : base(string.Join(" ", errores.SelectMany(e => e.Value).Distinct()))
    {
        Errores = errores;
    }
}
