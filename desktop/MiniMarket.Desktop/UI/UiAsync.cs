namespace MiniMarket.Desktop.UI;

/// <summary>
/// Ejecuta una operación asíncrona desde un evento de UI: cursor de espera, bloqueo de reentrada
/// (doble clic en "Cobrar" no crea dos ventas) y manejo uniforme de errores de la Api.
/// </summary>
public static class UiAsync
{
    private static readonly HashSet<Control> Ocupados = new();

    public static async Task<bool> EjecutarAsync(this Control control, Func<Task> accion)
    {
        var raiz = control.FindForm() ?? control;
        if (!Ocupados.Add(raiz)) return false;

        raiz.UseWaitCursor = true;
        Cursor.Current = Cursors.WaitCursor;
        try
        {
            await accion();
            return true;
        }
        catch (OperationCanceledException)
        {
            // El usuario desistió (p. ej. respondió "No" a una confirmación dentro de la acción).
            return false;
        }
        catch (Exception ex)
        {
            if (!raiz.IsDisposed) Dialogs.Excepcion(raiz, ex);
            return false;
        }
        finally
        {
            Ocupados.Remove(raiz);
            if (!raiz.IsDisposed) raiz.UseWaitCursor = false;
            Cursor.Current = Cursors.Default;
        }
    }
}
