using MiniMarket.Domain.Entities.Common;

namespace MiniMarket.Domain.Entities;

/// <summary>
/// Fix #2: el producto pertenece a la EMPRESA (catálogo compartido entre sucursales),
/// ya no a una sola Sucursal como en el script original. El stock por sucursal vive en <see cref="Inventario"/>.
/// </summary>
public class Producto : CatalogoEntity
{
    public int CategoriaId { get; set; }
    public int? ProveedorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? CodigoBarras { get; set; }
    public string? CodigoInterno { get; set; }
    /// <summary>Ruta relativa del archivo de imagen en el sistema de archivos; solo la ruta se guarda en BD.</summary>
    public string? ImagenPath { get; set; }
    /// <summary>Unidad en la que se lleva el inventario (ej. "unidad", "kg", "litro"). Todas las conversiones de TipoPrecio son hacia esta unidad.</summary>
    public string UnidadBase { get; set; } = "unidad";
    // TasaImpuesto se eliminó de aquí (migración 0003): el IVA es un único mantenimiento por
    // empresa (Empresa.TasaImpuesto, editable en Configuración), no se ingresa por producto.
}

/// <summary>
/// Presentaciones/tipos de precio de un producto (Unidad, Docena, Mayoreo, Granel, ...).
/// Fix #7: CantidadBase pasa de INT a DECIMAL para soportar productos a granel (ej. 0.5 kg),
/// y se garantiza un único EsDefault=true por producto vía índice único filtrado en BD.
/// </summary>
public class TipoPrecio
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Factor de conversión hacia la unidad base del producto (ej. Docena = 12, Caja = 24, Granel = 1).</summary>
    public decimal CantidadBase { get; set; }
    /// <summary>Precio FINAL que paga el cliente, IVA incluido (migración 0003) — es lo que se cobra
    /// tal cual en el POS. El desglose sin IVA se calcula "hacia adentro" en VentaService usando
    /// Empresa.TasaImpuesto, no se suma encima de este precio.</summary>
    public decimal PrecioVenta { get; set; }
    public decimal? PrecioCompra { get; set; }
    public bool EsDefault { get; set; }
    public string Estado { get; set; } = "A";
}
