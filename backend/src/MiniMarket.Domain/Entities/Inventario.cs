namespace MiniMarket.Domain.Entities;

/// <summary>
/// Fix #1: en el script original esta tabla no tenía SucursalId, por lo que era imposible
/// llevar stock por sucursal. Ahora la clave natural es (ProductoId, SucursalId).
/// </summary>
public class Inventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public int SucursalId { get; set; }
    public decimal StockActual { get; set; }
    public decimal StockMinimo { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>
/// Fix #3: se agregan EmpresaId/SucursalId/UsuarioId (ausentes en el original), StockResultante
/// (saldo posterior al movimiento, para trazabilidad/auditoría) y una referencia estructurada al
/// documento origen (DocumentoOrigenTipo/DocumentoOrigenId) en vez del campo de texto libre "referencia".
/// </summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int SucursalId { get; set; }
    public int ProductoId { get; set; }
    public int UsuarioId { get; set; }
    /// <summary>Persistido como texto corto validado con CHECK constraint (ver TipoMovimientoInventario).</summary>
    public string TipoMovimiento { get; set; } = string.Empty;
    /// <summary>Cantidad en unidad base; positiva para entradas, negativa para salidas.</summary>
    public decimal Cantidad { get; set; }
    public decimal StockResultante { get; set; }
    public string? DocumentoOrigenTipo { get; set; }
    public int? DocumentoOrigenId { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaMovimiento { get; set; }
}
