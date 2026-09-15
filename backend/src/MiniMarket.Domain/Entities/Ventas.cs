namespace MiniMarket.Domain.Entities;

/// <summary>
/// Fix #8: se agrega Folio (correlativo por sucursal), Estado (para anular sin borrar) y el
/// desglose Subtotal/DescuentoTotal/ImpuestoTotal/Total (el original solo tenía "total").
/// </summary>
public class Venta
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int CajaId { get; set; }
    public int? ClienteId { get; set; }
    public int UsuarioId { get; set; }
    public int Folio { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DescuentoTotal { get; set; }
    public decimal ImpuestoTotal { get; set; }
    public decimal Total { get; set; }
    /// <summary>"COMPLETADA" | "ANULADA".</summary>
    public string Estado { get; set; } = "COMPLETADA";
    public string? MotivoAnulacion { get; set; }
    public int? UsuarioAnulacionId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
}

public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public int TipoPrecioId { get; set; }
    /// <summary>Cantidad en la unidad de venta elegida (ej. 2 docenas), no en unidad base.</summary>
    public decimal Cantidad { get; set; }
    /// <summary>Cantidad convertida a unidad base = Cantidad * TipoPrecio.CantidadBase (para trazabilidad del cálculo).</summary>
    public decimal CantidadBaseCalculada { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Descuento { get; set; }
    public decimal Subtotal { get; set; }
}

/// <summary>Permite múltiples métodos de pago por venta (requisito 1).</summary>
public class PagoVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    /// <summary>"EFECTIVO" | "TARJETA" | "TRANSFERENCIA", validado con CHECK constraint.</summary>
    public string Metodo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    /// <summary>Número de autorización/voucher para tarjeta o transferencia.</summary>
    public string? Referencia { get; set; }
    public DateTime Fecha { get; set; }
}
