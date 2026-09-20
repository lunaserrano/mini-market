namespace MiniMarket.Domain.Security;

/// <summary>Tipos de evento registrados en EventoSeguridad.Tipo.</summary>
public static class TipoEventoSeguridad
{
    public const string LoginOk = "LOGIN_OK";
    public const string LoginFallido = "LOGIN_FALLIDO";
    public const string CuentaBloqueada = "CUENTA_BLOQUEADA";
    public const string Desbloqueo = "DESBLOQUEO";
    public const string Logout = "LOGOUT";
    public const string PasswordCambiada = "PASSWORD_CAMBIADA";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string SesionesRevocadas = "SESIONES_REVOCADAS";
    public const string UsuarioCreado = "USUARIO_CREADO";
    public const string UsuarioEditado = "USUARIO_EDITADO";
    public const string UsuarioEstado = "USUARIO_ESTADO";
    public const string RolCreado = "ROL_CREADO";
    public const string RolEditado = "ROL_EDITADO";
    public const string RolEliminado = "ROL_ELIMINADO";
    public const string RefreshReuso = "REFRESH_REUSO";
    /// <summary>Petición HTTP atendida por el backend (cualquier método y endpoint).</summary>
    public const string AccionApi = "ACCION_API";
    /// <summary>Clic del usuario en la interfaz.</summary>
    public const string ClicUi = "CLIC_UI";
    /// <summary>Cambio de pantalla en la interfaz.</summary>
    public const string NavegacionUi = "NAVEGACION_UI";

    public static readonly IReadOnlyList<string> Todos = new[]
    {
        LoginOk, LoginFallido, CuentaBloqueada, Desbloqueo, Logout, PasswordCambiada, PasswordReset,
        SesionesRevocadas, UsuarioCreado, UsuarioEditado, UsuarioEstado, RolCreado, RolEditado, RolEliminado,
        RefreshReuso, AccionApi, ClicUi, NavegacionUi
    };

    /// <summary>Tipos que puede reportar el navegador. Cualquier otro se rechaza: el cliente no puede falsear eventos de seguridad.</summary>
    public static readonly IReadOnlyList<string> PermitidosDesdeCliente = new[] { ClicUi, NavegacionUi };
}
