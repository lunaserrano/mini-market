namespace MiniMarket.Domain.Entities;

public class Compra
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    /// <summary>Sucursal destino del stock ingresado.</summary>
    public int SucursalId { get; set; }
    public int ProveedorId { get; set; }
    public int UsuarioId { get; set; }
    public string? NumeroDocumentoProveedor { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ImpuestoTotal { get; set; }
    public decimal Total { get; set; }
    /// <summary>"COMPLETADA" | "ANULADA".</summary>
    public string Estado { get; set; } = "COMPLETADA";
}

public class DetalleCompra
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int ProductoId { get; set; }
    /// <summary>Cantidad ingresada en la presentación indicada por el proveedor (ej. cajas).</summary>
    public decimal Cantidad { get; set; }
    /// <summary>Cantidad convertida a unidad base, la que realmente se suma a Inventario.</summary>
    public decimal CantidadBaseCalculada { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
