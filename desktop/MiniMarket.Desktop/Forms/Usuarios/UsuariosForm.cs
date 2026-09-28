namespace MiniMarket.Desktop.Forms.Usuarios;

/// <summary>
/// Usuarios de la empresa: alta, edición, activar/desactivar, restablecer contraseña, desbloquear
/// cuenta (tras intentos fallidos) y revocar sesiones abiertas.
/// </summary>
public sealed class UsuariosForm : ListForm<UsuarioDto>
{
    private readonly UsuariosApi _api;
    private readonly RolesApi _roles;
    private readonly SessionService _sesion;

    public UsuariosForm(UsuariosApi api, RolesApi roles, SessionService sesion) : base("Usuarios")
    {
        _api = api;
        _roles = roles;
        _sesion = sesion;
        BtnDesactivar.Visible = false;

        var acciones = new List<Control>();
        if (_sesion.Tiene(Permisos.UsuariosCambiarEstado))
            acciones.Add(Theme.Boton("Activar / desactivar", async (_, _) => await ConSeleccionAsync(CambiarEstadoAsync)));
        if (_sesion.Tiene(Permisos.UsuariosResetPassword))
            acciones.Add(Theme.Boton("Restablecer contraseña", async (_, _) => await ConSeleccionAsync(ResetPasswordAsync)));
        if (_sesion.Tiene(Permisos.UsuariosDesbloquear))
            acciones.Add(Theme.Boton("Desbloquear", async (_, _) => await ConSeleccionAsync(DesbloquearAsync)));
        if (_sesion.Tiene(Permisos.UsuariosEditar))
            acciones.Add(Theme.Boton("Revocar sesiones", async (_, _) => await ConSeleccionAsync(RevocarAsync)));
        for (var i = 0; i < acciones.Count; i++)
        {
            Barra.Controls.Add(acciones[i]);
            Barra.Controls.SetChildIndex(acciones[i], 3 + i);
        }
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.UsuariosCrear);
    protected override bool PuedeEditar => _sesion.Tiene(Permisos.UsuariosEditar);
    protected override bool TieneEstado => true;
    protected override bool EsInactivo(UsuarioDto item) => item.Estado != "A";
    protected override Color? ColorFila(UsuarioDto item) => item.Bloqueado ? Theme.FilaAlerta : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(UsuarioDto.NombreCompleto), 220, relleno: true);
        grid.Col("Usuario", nameof(UsuarioDto.Username), 130);
        grid.Col("Rol", nameof(UsuarioDto.RolNombre), 140);
        grid.Col("Sucursal", nameof(UsuarioDto.SucursalId), 80);
        grid.Col("Estado", nameof(UsuarioDto.Estado), 90, FormatoColumna.Estado);
        grid.Col("Bloqueado", nameof(UsuarioDto.Bloqueado), 80, FormatoColumna.SiNo);
        grid.Col("Debe cambiar clave", nameof(UsuarioDto.DebeCambiarPassword), 120, FormatoColumna.SiNo);
        grid.Col("Último ingreso", nameof(UsuarioDto.UltimoLoginUtc), 150, FormatoColumna.FechaUtc);
    }

    protected override async Task<IReadOnlyList<UsuarioDto>> ObtenerAsync() => await _api.ListarAsync();

    protected override bool Coincide(UsuarioDto i, string t) =>
        Contiene(i.NombreCompleto, t) || Contiene(i.Username, t) || Contiene(i.RolNombre, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(UsuarioDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(UsuarioDto? usuario)
    {
        var roles = await _roles.ListarAsync();
        using var dlg = new EditDialog(usuario is null ? "Nuevo usuario" : $"Editar usuario: {usuario.Username}");
        var nombre = dlg.AgregarTexto("Nombre completo *", usuario?.NombreCompleto, maxLength: 150);
        var username = dlg.AgregarTexto("Usuario *", usuario?.Username, maxLength: 50);
        username.Enabled = usuario is null;
        var rol = dlg.AgregarCombo("Rol *", roles, r => r.Nombre, r => r.Id == usuario?.RolId);
        var sucursal = dlg.AgregarNumero("Sucursal (Id)", usuario?.SucursalId ?? _sesion.SucursalId ?? 1, minimo: 1, maximo: 100_000);
        TextBox? password = null;
        CheckBox? debeCambiar = null;
        if (usuario is null)
        {
            password = dlg.AgregarTexto("Contraseña inicial *", "", password: true, maxLength: 128);
            debeCambiar = dlg.AgregarCheck("Debe cambiar la contraseña al ingresar", true);
        }

        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.Validar(() => string.IsNullOrWhiteSpace(username.Text) ? "El usuario es obligatorio." : null);
        dlg.Validar(() => rol.Seleccion<RolDto>() is null ? "Seleccione un rol." : null);
        dlg.Validar(() => password is not null && password.Text.Length == 0 ? "La contraseña inicial es obligatoria." : null);
        dlg.AlGuardar(async () =>
        {
            var rolId = rol.Seleccion<RolDto>()!.Id;
            if (usuario is null)
                await _api.CrearAsync(new UsuarioCreateDto((int)sucursal.Value, nombre.Text.Trim(), username.Text.Trim(), password!.Text, rolId, debeCambiar!.Checked));
            else
                await _api.ActualizarAsync(usuario.Id, new UsuarioUpdateDto((int)sucursal.Value, nombre.Text.Trim(), rolId));
        });
        if (dlg.ShowDialog(this) == DialogResult.OK) await CargarAsync();
    }

    private Task ConSeleccionAsync(Func<UsuarioDto, Task> accion)
    {
        if (Grid.Seleccionado<UsuarioDto>() is not { } u)
        {
            Dialogs.Info(this, "Seleccione un usuario.");
            return Task.CompletedTask;
        }
        return this.EjecutarAsync(() => accion(u));
    }

    private async Task CambiarEstadoAsync(UsuarioDto u)
    {
        var activar = u.Estado != "A";
        if (!activar && u.Id == _sesion.Usuario?.Id)
        {
            Dialogs.Aviso(this, "No puede desactivar su propio usuario.");
            return;
        }
        if (!Dialogs.Confirmar(this, $"¿{(activar ? "Activar" : "Desactivar")} al usuario {u.Username}?")) return;
        await _api.CambiarEstadoAsync(u.Id, activar);
        await CargarAsync();
    }

    private async Task ResetPasswordAsync(UsuarioDto u)
    {
        using var dlg = new EditDialog($"Restablecer contraseña: {u.Username}");
        var nueva = dlg.AgregarTexto("Contraseña nueva *", "", password: true, maxLength: 128);
        var confirmar = dlg.AgregarTexto("Confirmar *", "", password: true, maxLength: 128);
        var debeCambiar = dlg.AgregarCheck("Debe cambiarla al ingresar", true);
        dlg.Validar(() => nueva.Text.Length == 0 ? "Ingrese la contraseña." : null);
        dlg.Validar(() => nueva.Text != confirmar.Text ? "La confirmación no coincide." : null);
        dlg.AlGuardar(() => _api.ResetPasswordAsync(u.Id, new ResetPasswordRequest(nueva.Text, debeCambiar.Checked)));
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Dialogs.Info(this, "Contraseña restablecida. Se cerraron las sesiones abiertas del usuario.");
        await CargarAsync();
    }

    private async Task DesbloquearAsync(UsuarioDto u)
    {
        if (!u.Bloqueado)
        {
            Dialogs.Info(this, "El usuario no está bloqueado.");
            return;
        }
        await _api.DesbloquearAsync(u.Id);
        await CargarAsync();
    }

    private async Task RevocarAsync(UsuarioDto u)
    {
        if (!Dialogs.Confirmar(this, $"¿Cerrar todas las sesiones abiertas de {u.Username}? Deberá volver a iniciar sesión en todos sus equipos.")) return;
        await _api.RevocarSesionesAsync(u.Id);
        Dialogs.Info(this, "Sesiones revocadas.");
    }
}
