namespace MiniMarket.Domain.Entities;

/// <summary>
/// Deuda de un cliente originada en una venta (1:1 con <see cref="Venta"/>): la venta se entrega
/// sin cobrarse del todo y el cliente abona hasta cancelarla. MontoOriginal es lo que quedó
/// pendiente al vender (Total - pagos recibidos en el momento); SaldoPendiente baja con cada abono.
/// </summary>
public class Credito
{
    public const string Pendiente = "PENDIENTE";
    public const string Pagado = "PAGADO";
    public const string Anulado = "ANULADO";

    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int VentaId { get; set; }
    public int ClienteId { get; set; }
    public decimal MontoOriginal { get; set; }
    public decimal SaldoPendiente { get; set; }
    /// <summary>"PENDIENTE" | "PAGADO" | "ANULADO", validado con CHECK constraint.</summary>
    public string Estado { get; set; } = Pendiente;
    public DateTime? FechaVencimiento { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int CreadoPorUsuarioId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
}

/// <summary>Pago posterior a la venta sobre un crédito. Inmutable: no se edita ni se elimina.</summary>
public class AbonoCredito
{
    public int Id { get; set; }
    public int CreditoId { get; set; }
    /// <summary>Caja que recibió el abono; solo se llena en abonos en efectivo.</summary>
    public int? CajaId { get; set; }
    public int UsuarioId { get; set; }
    /// <summary>"EFECTIVO" | "TARJETA" | "TRANSFERENCIA", validado con CHECK constraint.</summary>
    public string Metodo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? Referencia { get; set; }
    public DateTime Fecha { get; set; }
}
