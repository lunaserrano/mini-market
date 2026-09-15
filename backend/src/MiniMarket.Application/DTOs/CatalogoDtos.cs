namespace MiniMarket.Application.DTOs;

public record CategoriaDto(int Id, string Nombre, string? Descripcion, string Estado);
public record CategoriaCreateDto(string Nombre, string? Descripcion);
public record CategoriaUpdateDto(string Nombre, string? Descripcion);

public record ProveedorDto(int Id, string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, string? IdentificacionFiscal, string Estado);
public record ProveedorCreateDto(string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, string? IdentificacionFiscal);
public record ProveedorUpdateDto(string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, string? IdentificacionFiscal);

public record ClienteDto(int Id, string Nombre, string? IdentificacionFiscal, string? Telefono, string? Email, string? Direccion, string Estado);
public record ClienteCreateDto(string Nombre, string? IdentificacionFiscal, string? Telefono, string? Email, string? Direccion);
public record ClienteUpdateDto(string Nombre, string? IdentificacionFiscal, string? Telefono, string? Email, string? Direccion);
