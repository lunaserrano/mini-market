using MiniMarket.Application.Interfaces;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Tests.Helpers;

/// <summary>Reloj controlable para probar bloqueos y expiraciones sin esperas reales.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _ahora;

    public ManualTimeProvider(DateTimeOffset? inicio = null) => _ahora = inicio ?? new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _ahora;

    public DateTime AhoraUtc => _ahora.UtcDateTime;

    public void Avanzar(TimeSpan tiempo) => _ahora = _ahora.Add(tiempo);
}

public static class Datos
{
    public const int EmpresaId = 1;
    public const int ActorId = 10;

    public static ITenantContext Tenant(int usuarioId = ActorId, int empresaId = EmpresaId, params string[] permisos)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.EmpresaId.Returns(empresaId);
        tenant.UsuarioId.Returns(usuarioId);
        tenant.Permisos.Returns(permisos);
        tenant.TienePermiso(Arg.Any<string>()).Returns(ci => permisos.Contains(ci.Arg<string>()));
        return tenant;
    }

    public static RolCatalogo Rol(int id = 2, string codigo = "supervisor", string nombre = "Supervisor", bool esSistema = true, int empresaId = EmpresaId) =>
        new() { Id = id, EmpresaId = empresaId, Codigo = codigo, Nombre = nombre, EsSistema = esSistema, Estado = "A" };

    public static RolCatalogo RolAdmin(int id = 1) => Rol(id, RolCatalogo.CodigoAdmin, "Administrador");

    public static Usuario Usuario(int id = 20, int rolId = 2, string estado = "A", string username = "maria") =>
        new()
        {
            Id = id,
            EmpresaId = EmpresaId,
            RolId = rolId,
            Estado = estado,
            Username = username,
            NombreCompleto = "María Pérez",
            PasswordHash = "hash-guardado"
        };
}
