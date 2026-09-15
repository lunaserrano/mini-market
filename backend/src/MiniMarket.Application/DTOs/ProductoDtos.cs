namespace MiniMarket.Application.DTOs;

/// <summary>PrecioVenta es el precio FINAL con IVA incluido (lo que se cobra en el POS) — ver
/// migración 0003. El frontend calcula/muestra el precio sin IVA como ayuda visual, pero solo
/// PrecioVenta se persiste.</summary>
public record TipoPrecioDto(int Id, string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault, string Estado);
public record TipoPrecioCreateDto(string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault);
public record TipoPrecioUpdateDto(string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault);

public record ProductoDto(
    int Id,
    int CategoriaId,
    string? CategoriaNombre,
    int? ProveedorId,
    string Nombre,
    string? Descripcion,
    string? CodigoBarras,
    string? CodigoInterno,
    string? ImagenPath,
    string UnidadBase,
    string Estado,
    IReadOnlyList<TipoPrecioDto> TiposPrecio
);

public record ProductoCreateDto(
    int CategoriaId,
    int? ProveedorId,
    string Nombre,
    string? Descripcion,
    string? CodigoBarras,
    string? CodigoInterno,
    string? ImagenPath,
    string UnidadBase,
    IReadOnlyList<TipoPrecioCreateDto> TiposPrecio
);

public record ProductoUpdateDto(
    int CategoriaId,
    int? ProveedorId,
    string Nombre,
    string? Descripcion,
    string? CodigoBarras,
    string? CodigoInterno,
    string? ImagenPath,
    string UnidadBase
);

/// <summary>Proyección optimizada para el buscador del POS: producto + sus tipos de precio + stock en la sucursal activa.</summary>
public record ProductoPosDto(
    int ProductoId,
    string Nombre,
    string? CodigoBarras,
    string UnidadBase,
    decimal StockActual,
    IReadOnlyList<TipoPrecioDto> TiposPrecio
);
