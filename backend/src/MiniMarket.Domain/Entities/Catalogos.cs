using MiniMarket.Domain.Entities.Common;

namespace MiniMarket.Domain.Entities;

public class Categoria : CatalogoEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class Proveedor : CatalogoEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? IdentificacionFiscal { get; set; }
}

/// <summary>Tabla nueva (fix #6): el frontend ya modelaba Cliente pero no existía en el script original.</summary>
public class Cliente : CatalogoEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? IdentificacionFiscal { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
}
