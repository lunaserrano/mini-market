namespace MiniMarket.Domain.Entities.Common;

/// <summary>
/// Propiedades de auditoría compartidas. Al usar Dapper (sin SaveChanges/interceptores de EF),
/// estas columnas se completan explícitamente en cada repositorio de escritura
/// (Infrastructure.Persistence.Repositories), nunca automáticamente.
/// </summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }
    public int? CreadoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? ModificadoPorUsuarioId { get; set; }
    public DateTime? FechaModificacion { get; set; }
}

/// <summary>Entidad de catálogo/maestro con soft delete vía Estado ('A'/'I').</summary>
public abstract class CatalogoEntity : AuditableEntity
{
    public int EmpresaId { get; set; }
    public string Estado { get; set; } = "A";
}
