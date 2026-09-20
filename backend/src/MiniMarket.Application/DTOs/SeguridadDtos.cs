namespace MiniMarket.Application.DTOs;

public record RolDto(int Id, string Codigo, string Nombre, string? Descripcion, bool EsSistema, int TotalUsuarios, int TotalPermisos);

public record RolDetalleDto(int Id, string Codigo, string Nombre, string? Descripcion, bool EsSistema, int TotalUsuarios, IReadOnlyList<string> Permisos);

public record RolCreateDto(string Nombre, string? Descripcion, IReadOnlyList<string> Permisos);

public record RolUpdateDto(string Nombre, string? Descripcion, IReadOnlyList<string> Permisos);

public record PermisoDto(string Codigo, string Modulo, string Nombre);

public record PermisoModuloDto(string Modulo, IReadOnlyList<PermisoDto> Permisos);

public record EventoSeguridadDto(
    long Id,
    DateTime FechaUtc,
    string Tipo,
    string? Detalle,
    int? ActorUsuarioId,
    string? ActorNombre,
    int? UsuarioObjetivoId,
    string? ObjetivoNombre,
    string? Ip,
    string Origen,
    string? Metodo,
    string? Ruta,
    short? StatusCode,
    int? DuracionMs,
    string? Datos
);

/// <param name="Origen">SEG, API o UI (ver OrigenEvento). Null = todos.</param>
public record AuditoriaFiltro(DateTime? Desde, DateTime? Hasta, int? UsuarioId, string? Tipo, int Pagina = 1, int TamanoPagina = 25, string? Origen = null);

/// <summary>Acción del usuario en el navegador (clic, cambio de pantalla) reportada por el frontend.</summary>
/// <param name="Datos">Descriptor del elemento (etiqueta, id, texto...). Nunca valores de campos de formulario.</param>
public record EventoClienteDto(string Tipo, DateTime? FechaUtc, string? Ruta, string? Detalle, Dictionary<string, string?>? Datos);

public record PaginaResultado<T>(IReadOnlyList<T> Items, int Total, int Pagina, int TamanoPagina);
