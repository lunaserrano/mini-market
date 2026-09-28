using MiniMarket.Desktop.Api.Models;

namespace MiniMarket.Desktop.Services;

/// <summary>
/// Estado de la sesión del usuario en el cliente: tokens (en memoria), usuario, permisos y empresa.
/// El refresh token solo se persiste (cifrado, ver <see cref="TokenStore"/>) si el usuario marcó
/// "Recordar sesión".
/// </summary>
public sealed class SessionService
{
    private readonly TokenStore _tokenStore;
    private readonly object _sync = new();
    private HashSet<string> _permisos = new(StringComparer.Ordinal);

    public SessionService(TokenStore tokenStore) => _tokenStore = tokenStore;

    public string? AccessToken { get; private set; }
    public DateTime AccessTokenExpiraUtc { get; private set; }
    public string? RefreshToken { get; private set; }
    public UsuarioActualDto? Usuario { get; private set; }
    public EmpresaDto? Empresa { get; set; }
    public bool Recordar { get; private set; }

    public bool Activa => Usuario is not null && RefreshToken is not null;

    /// <summary>Margen para renovar antes de que venza (evita 401 en peticiones en vuelo).</summary>
    public bool AccessTokenPorVencer => AccessToken is not null && DateTime.UtcNow >= AccessTokenExpiraUtc.AddSeconds(-30);

    /// <summary>Se dispara (desde cualquier hilo) cuando el refresh token ya no es válido.</summary>
    public event EventHandler? SesionExpirada;

    public void Iniciar(LoginResponse login, bool recordar)
    {
        Recordar = recordar;
        ActualizarTokens(login);
        if (!recordar) _tokenStore.Borrar();
    }

    public void ActualizarTokens(LoginResponse login)
    {
        lock (_sync)
        {
            AccessToken = login.Token;
            AccessTokenExpiraUtc = DateTime.SpecifyKind(login.ExpiraUtc, DateTimeKind.Utc);
            RefreshToken = login.RefreshToken;
            Usuario = login.Usuario;
            _permisos = new HashSet<string>(login.Usuario.Permisos, StringComparer.Ordinal);
        }
        if (Recordar) _tokenStore.Guardar(login.RefreshToken);
    }

    public void Expirar()
    {
        var estabaActiva = Activa;
        Cerrar();
        if (estabaActiva) SesionExpirada?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        lock (_sync)
        {
            AccessToken = null;
            RefreshToken = null;
            Usuario = null;
            Empresa = null;
            _permisos = new HashSet<string>(StringComparer.Ordinal);
        }
        _tokenStore.Borrar();
    }

    /// <summary>True si el usuario tiene AL MENOS UNO de los permisos (misma semántica que el frontend web y [HasPermission]).</summary>
    public bool Tiene(params string[] permisos) => permisos.Any(_permisos.Contains);

    public int? SucursalId => Usuario?.SucursalId;
}
