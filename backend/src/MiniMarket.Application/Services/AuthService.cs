using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;

namespace MiniMarket.Application.Services;

public class AuthService
{
    private const string MensajeSesionInvalida = "La sesión no es válida. Inicie sesión nuevamente.";

    private readonly IUsuarioRepository _usuarios;
    private readonly IRolRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly PermisoService _permisos;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IRefreshTokenGenerator _refreshGenerator;
    private readonly ISeguridadAuditor _auditor;
    private readonly IRequestInfo _request;
    private readonly ITenantContext _tenant;
    private readonly SeguridadOptions _options;
    private readonly TimeProvider _time;

    public AuthService(
        IUsuarioRepository usuarios,
        IRolRepository roles,
        IRefreshTokenRepository refreshTokens,
        PermisoService permisos,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        IRefreshTokenGenerator refreshGenerator,
        ISeguridadAuditor auditor,
        IRequestInfo request,
        ITenantContext tenant,
        SeguridadOptions options,
        TimeProvider time)
    {
        _usuarios = usuarios;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _permisos = permisos;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
        _refreshGenerator = refreshGenerator;
        _auditor = auditor;
        _request = request;
        _tenant = tenant;
        _options = options;
        _time = time;
    }

    private DateTime Ahora => _time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Login multi-tenant: como el username es único por empresa (no global), en este esqueleto de
    /// una sola empresa se asume EmpresaId=1; una evolución SaaS real pediría también el "slug" o
    /// dominio de la empresa en el request de login para resolver el EmpresaId antes de buscar el usuario.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, int empresaId = 1)
    {
        var ahora = Ahora;
        var username = request.Username.Trim();
        var usuario = await _usuarios.ObtenerPorUsernameAsync(empresaId, username);

        if (usuario is null)
        {
            // Verifica contra un hash falso para que el tiempo de respuesta no delate si el usuario existe.
            _passwordHasher.SimularVerificacion(request.Password);
            await _auditor.RegistrarAsync(TipoEventoSeguridad.LoginFallido, empresaId, null, null, $"Usuario inexistente: {Truncar(username, 50)}");
            throw new CredencialesInvalidasException();
        }

        if (usuario.EstaBloqueado(ahora))
        {
            await _auditor.RegistrarAsync(TipoEventoSeguridad.LoginFallido, empresaId, usuario.Id, usuario.Id, "Intento con la cuenta bloqueada");
            throw new CuentaBloqueadaException(usuario.BloqueadoHasta!.Value);
        }

        if (!_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            var bloqueadoHasta = await _usuarios.RegistrarIntentoFallidoAsync(
                empresaId, usuario.Id, _options.MaxIntentosFallidos, ahora.AddMinutes(_options.MinutosBloqueo));

            if (bloqueadoHasta is not null)
            {
                await _auditor.RegistrarAsync(TipoEventoSeguridad.CuentaBloqueada, empresaId, usuario.Id, usuario.Id,
                    $"Bloqueada hasta {bloqueadoHasta:u} tras {_options.MaxIntentosFallidos} intentos fallidos");
                throw new CuentaBloqueadaException(bloqueadoHasta.Value);
            }

            await _auditor.RegistrarAsync(TipoEventoSeguridad.LoginFallido, empresaId, usuario.Id, usuario.Id, "Contraseña incorrecta");
            throw new CredencialesInvalidasException();
        }

        if (usuario.Estado != "A")
        {
            await _auditor.RegistrarAsync(TipoEventoSeguridad.LoginFallido, empresaId, usuario.Id, usuario.Id, "Usuario inactivo");
            throw new CredencialesInvalidasException("El usuario está inactivo.");
        }

        var rol = await _roles.ObtenerPorIdAsync(usuario.EmpresaId, usuario.RolId)
            ?? throw new CredencialesInvalidasException("El usuario no tiene un rol válido asignado.");

        await _usuarios.RegistrarLoginOkAsync(usuario.EmpresaId, usuario.Id, ahora);
        await _refreshTokens.PurgarAntiguosAsync(usuario.Id, ahora.AddDays(-30));

        var (respuesta, _) = await EmitirSesionAsync(usuario, rol, Guid.NewGuid(), ahora);
        await _auditor.RegistrarAsync(TipoEventoSeguridad.LoginOk, usuario.EmpresaId, usuario.Id, usuario.Id);
        return respuesta;
    }

    /// <summary>
    /// Renueva la sesión con rotación: el refresh token usado se revoca y se emite uno nuevo de la misma
    /// familia. Presentar de nuevo un token ya rotado indica que fue robado/copiado: se revoca toda la familia.
    /// Los permisos se recalculan con el estado actual del rol, así los cambios de permisos llegan al renovar.
    /// </summary>
    public async Task<LoginResponse> RefrescarAsync(RefreshRequest request)
    {
        var ahora = Ahora;
        var actual = await _refreshTokens.ObtenerPorHashAsync(_refreshGenerator.Hashear(request.RefreshToken))
            ?? throw new CredencialesInvalidasException(MensajeSesionInvalida);

        var usuario = await _usuarios.ObtenerParaSesionAsync(actual.UsuarioId);

        if (actual.ReemplazadoPorId is not null)
        {
            await _refreshTokens.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            await _auditor.RegistrarAsync(TipoEventoSeguridad.RefreshReuso, usuario?.EmpresaId, actual.UsuarioId, actual.UsuarioId,
                "Se reutilizó un refresh token ya rotado; se revocó toda la familia de sesiones");
            throw new CredencialesInvalidasException(MensajeSesionInvalida);
        }

        if (actual.EstaRevocado || actual.HaExpirado(ahora))
            throw new CredencialesInvalidasException(MensajeSesionInvalida);

        if (usuario is null || usuario.Estado != "A" || usuario.EstaBloqueado(ahora))
        {
            await _refreshTokens.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            throw new CredencialesInvalidasException(MensajeSesionInvalida);
        }

        var rol = await _roles.ObtenerPorIdAsync(usuario.EmpresaId, usuario.RolId)
            ?? throw new CredencialesInvalidasException(MensajeSesionInvalida);

        // Si otra petición ya rotó este mismo token (p. ej. dos pestañas a la vez), esta pierde la carrera.
        if (!await _refreshTokens.RevocarAsync(actual.Id, ahora))
            throw new CredencialesInvalidasException(MensajeSesionInvalida);

        var (respuesta, nuevoId) = await EmitirSesionAsync(usuario, rol, actual.FamiliaId, ahora);
        await _refreshTokens.MarcarReemplazoAsync(actual.Id, nuevoId);
        return respuesta;
    }

    public async Task LogoutAsync(LogoutRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var token = await _refreshTokens.ObtenerPorHashAsync(_refreshGenerator.Hashear(request.RefreshToken));
            // Solo se puede cerrar una sesión propia.
            if (token is not null && token.UsuarioId == _tenant.UsuarioId)
                await _refreshTokens.RevocarAsync(token.Id, Ahora);
        }

        await _auditor.RegistrarAsync(TipoEventoSeguridad.Logout, _tenant.EmpresaId, _tenant.UsuarioId, _tenant.UsuarioId);
    }

    /// <summary>
    /// Cambio de contraseña propio. Revoca todas las sesiones existentes y devuelve una sesión nueva
    /// (con DebeCambiarPassword ya en false, que es lo que libera al usuario del bloqueo por cambio forzado).
    /// </summary>
    public async Task<LoginResponse> CambiarPasswordAsync(CambiarPasswordRequest request)
    {
        var ahora = Ahora;
        var usuario = await _usuarios.ObtenerPorIdAsync(_tenant.EmpresaId, _tenant.UsuarioId)
            ?? throw new EntidadNoEncontradaException("Usuario", _tenant.UsuarioId);

        if (!_passwordHasher.Verify(request.PasswordActual, usuario.PasswordHash))
        {
            // Un token robado no debe servir para adivinar la contraseña actual sin límite.
            var bloqueadoHasta = await _usuarios.RegistrarIntentoFallidoAsync(
                usuario.EmpresaId, usuario.Id, _options.MaxIntentosFallidos, ahora.AddMinutes(_options.MinutosBloqueo));
            if (bloqueadoHasta is not null)
            {
                await _refreshTokens.RevocarTodosDeUsuarioAsync(usuario.Id, ahora);
                await _auditor.RegistrarAsync(TipoEventoSeguridad.CuentaBloqueada, usuario.EmpresaId, usuario.Id, usuario.Id,
                    "Bloqueada por intentos fallidos al cambiar la contraseña");
                throw new CuentaBloqueadaException(bloqueadoHasta.Value);
            }

            throw new ReglaDeNegocioException("La contraseña actual es incorrecta.");
        }

        if (_passwordHasher.Verify(request.PasswordNueva, usuario.PasswordHash))
            throw new ReglaDeNegocioException("La nueva contraseña debe ser distinta de la actual.");

        var rol = await _roles.ObtenerPorIdAsync(usuario.EmpresaId, usuario.RolId)
            ?? throw new EntidadNoEncontradaException("Rol", usuario.RolId);

        await _usuarios.ActualizarPasswordAsync(usuario.EmpresaId, usuario.Id, _passwordHasher.Hash(request.PasswordNueva), false, usuario.Id);
        await _refreshTokens.RevocarTodosDeUsuarioAsync(usuario.Id, ahora);
        await _auditor.RegistrarAsync(TipoEventoSeguridad.PasswordCambiada, usuario.EmpresaId, usuario.Id, usuario.Id);

        usuario.DebeCambiarPassword = false;
        var (respuesta, _) = await EmitirSesionAsync(usuario, rol, Guid.NewGuid(), ahora);
        return respuesta;
    }

    /// <summary>Datos del usuario autenticado con sus permisos leídos de la BD (no del token), para refrescar la UI.</summary>
    public async Task<UsuarioActualDto> ObtenerActualAsync()
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(_tenant.EmpresaId, _tenant.UsuarioId)
            ?? throw new EntidadNoEncontradaException("Usuario", _tenant.UsuarioId);
        var rol = await _roles.ObtenerPorIdAsync(usuario.EmpresaId, usuario.RolId)
            ?? throw new EntidadNoEncontradaException("Rol", usuario.RolId);

        return ConstruirUsuarioActual(usuario, rol, await _permisos.ResolverAsync(rol));
    }

    private async Task<(LoginResponse Respuesta, int RefreshTokenId)> EmitirSesionAsync(Usuario usuario, RolCatalogo rol, Guid familiaId, DateTime ahora)
    {
        var permisos = await _permisos.ResolverAsync(rol);
        var access = _jwt.Generar(usuario, rol, permisos);

        var (tokenClaro, hash) = _refreshGenerator.Generar();
        var refresh = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = hash,
            FamiliaId = familiaId,
            CreadoUtc = ahora,
            ExpiraUtc = ahora.AddDays(_options.RefreshTokenDays),
            Ip = _request.Ip
        };
        refresh.Id = await _refreshTokens.CrearAsync(refresh);

        var respuesta = new LoginResponse(access.Token, access.ExpiraUtc, tokenClaro, refresh.ExpiraUtc,
            ConstruirUsuarioActual(usuario, rol, permisos));
        return (respuesta, refresh.Id);
    }

    private static UsuarioActualDto ConstruirUsuarioActual(Usuario usuario, RolCatalogo rol, IReadOnlyList<string> permisos) =>
        new(usuario.Id, usuario.EmpresaId, usuario.SucursalId, usuario.NombreCompleto, usuario.Username,
            rol.Codigo, rol.Nombre, permisos, usuario.DebeCambiarPassword);

    private static string Truncar(string valor, int max) => valor.Length <= max ? valor : valor[..max];
}
