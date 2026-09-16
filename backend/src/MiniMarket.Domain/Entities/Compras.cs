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
    /// <summary>Presentación (TipoPrecio) en la que se compró — mismo catálogo que usan las Ventas
    /// (Unidad, Cartón x30, Caja x360...). Fix: antes se recibía un "factor" (CantidadBase) escrito
    /// libremente por el cliente sin validar contra nada; ahora se resuelve server-side igual que
    /// VentaService.CrearAsync (ver migración 0004, columna nullable por compatibilidad histórica).</summary>
    public int TipoPrecioId { get; set; }
    /// <summary>Cantidad ingresada en la presentación indicada por el proveedor (ej. cajas).</summary>
    public decimal Cantidad { get; set; }
    /// <summary>Cantidad convertida a unidad base, la que realmente se suma a Inventario.</summary>
    public decimal CantidadBaseCalculada { get; set; }
    /// <summary>Precio pagado por TODA la unidad de medida (ej. $30 la caja), no por unidad base.</summary>
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }
}
