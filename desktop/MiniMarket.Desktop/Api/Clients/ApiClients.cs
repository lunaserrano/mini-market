using MiniMarket.Desktop.Api.Models;
using static MiniMarket.Desktop.Api.ApiHttp;

namespace MiniMarket.Desktop.Api.Clients;

// Un cliente tipado por controller de MiniMarket.Api (backend/src/MiniMarket.Api/Controllers).
// Las rutas son relativas a ".../api/" de la Api embebida (ApiLocal.BaseAddress). Los permisos los valida la Api: la UI solo
// oculta lo que el usuario no puede usar.

public sealed class HealthApi(ApiHttp http)
{
    public Task<HealthDto> ObtenerAsync(CancellationToken ct = default) => http.GetAsync<HealthDto>("health", ct);
}

public sealed class AuthApi(ApiHttp http)
{
    public Task<LoginResponse> LoginAsync(string usuario, string password) =>
        http.PostAsync<LoginResponse>("auth/login", new LoginRequest(usuario, password));

    public Task<LoginResponse> RefreshAsync(string refreshToken) =>
        http.PostAsync<LoginResponse>("auth/refresh", new RefreshRequest(refreshToken));

    public Task LogoutAsync(string? refreshToken) => http.PostAsync("auth/logout", new LogoutRequest(refreshToken));

    public Task<LoginResponse> CambiarPasswordAsync(string actual, string nueva) =>
        http.PostAsync<LoginResponse>("auth/cambiar-password", new CambiarPasswordRequest(actual, nueva));

    public Task<UsuarioActualDto> MeAsync() => http.GetAsync<UsuarioActualDto>("auth/me");
}

public sealed class EmpresaApi(ApiHttp http)
{
    public Task<EmpresaDto> ObtenerActualAsync() => http.GetAsync<EmpresaDto>("empresa/actual");
    public Task<EmpresaDto> ActualizarAsync(EmpresaUpdateDto dto) => http.PutAsync<EmpresaDto>("empresa", dto);
}

/// <summary>CRUD de catálogo simple (categorías, clientes, proveedores): mismas rutas y semántica.</summary>
public abstract class CatalogoApi<TDto, TSave>(ApiHttp http, string ruta)
{
    public Task<List<TDto>> ListarAsync() => http.GetAsync<List<TDto>>(ruta);
    public Task<TDto> ObtenerAsync(int id) => http.GetAsync<TDto>($"{ruta}/{id}");
    public Task<int> CrearAsync(TSave dto) => http.PostAsync<int>(ruta, dto);
    public Task ActualizarAsync(int id, TSave dto) => http.PutAsync($"{ruta}/{id}", dto);
    /// <summary>Soft delete (Estado = 'I').</summary>
    public Task DesactivarAsync(int id) => http.DeleteAsync($"{ruta}/{id}");
}

public sealed class CategoriasApi(ApiHttp http) : CatalogoApi<CategoriaDto, CategoriaSaveDto>(http, "categorias");
public sealed class ClientesApi(ApiHttp http) : CatalogoApi<ClienteDto, ClienteSaveDto>(http, "clientes");
public sealed class ProveedoresApi(ApiHttp http) : CatalogoApi<ProveedorDto, ProveedorSaveDto>(http, "proveedores");

public sealed class ProductosApi(ApiHttp http)
{
    public Task<List<ProductoDto>> ListarAsync() => http.GetAsync<List<ProductoDto>>("productos");
    public Task<List<ProductoPosDto>> BuscarAsync(string termino, CancellationToken ct = default) =>
        http.GetAsync<List<ProductoPosDto>>(Query("productos/buscar", ("termino", termino)), ct);
    public Task<ProductoDto> ObtenerAsync(int id) => http.GetAsync<ProductoDto>($"productos/{id}");
    public Task<int> CrearAsync(ProductoCreateDto dto) => http.PostAsync<int>("productos", dto);
    public Task ActualizarAsync(int id, ProductoUpdateDto dto) => http.PutAsync($"productos/{id}", dto);
    public Task DesactivarAsync(int id) => http.DeleteAsync($"productos/{id}");

    public Task<int> AgregarTipoPrecioAsync(int productoId, TipoPrecioSaveDto dto) =>
        http.PostAsync<int>($"productos/{productoId}/tipos-precio", dto);
    public Task ActualizarTipoPrecioAsync(int productoId, int tipoPrecioId, TipoPrecioSaveDto dto) =>
        http.PutAsync($"productos/{productoId}/tipos-precio/{tipoPrecioId}", dto);
    public Task EliminarTipoPrecioAsync(int productoId, int tipoPrecioId) =>
        http.DeleteAsync($"productos/{productoId}/tipos-precio/{tipoPrecioId}");
}

public sealed class InventarioApi(ApiHttp http)
{
    public Task<List<InventarioDto>> ListarAsync(int? sucursalId = null, int? productoId = null) =>
        http.GetAsync<List<InventarioDto>>(Query("inventario", ("sucursalId", sucursalId), ("productoId", productoId)));
    public Task<InventarioDto?> ObtenerAsync(int productoId, int sucursalId) =>
        http.GetOrDefaultAsync<InventarioDto>($"inventario/{productoId}/sucursal/{sucursalId}");
    public Task AjustarAsync(AjusteInventarioRequest request) => http.PostAsync("inventario/ajuste", request);
    public Task ActualizarStockMinimoAsync(StockMinimoRequest request) => http.PutAsync("inventario/stock-minimo", request);
    public Task<List<MovimientoInventarioDto>> MovimientosAsync(int? productoId, int? sucursalId, DateTime? desde, DateTime? hasta) =>
        http.GetAsync<List<MovimientoInventarioDto>>(Query("inventario/movimientos",
            ("productoId", productoId), ("sucursalId", sucursalId), ("desde", desde), ("hasta", hasta)));
}

public sealed class CajaApi(ApiHttp http)
{
    /// <summary>Caja ABIERTA del usuario actual, o null si no tiene.</summary>
    public Task<CajaDto?> ObtenerActualAsync() => http.GetOrDefaultAsync<CajaDto>("caja/actual");
    public Task<List<CajaDto>> ListarAsync(int? sucursalId = null) => http.GetAsync<List<CajaDto>>(Query("caja", ("sucursalId", sucursalId)));
    public Task<CajaDto> AbrirAsync(decimal montoInicial) => http.PostAsync<CajaDto>("caja/apertura", new AperturaCajaRequest(montoInicial));
    public Task<CajaDto> CerrarAsync(int id, decimal montoDeclarado) => http.PostAsync<CajaDto>($"caja/{id}/cierre", new CierreCajaRequest(montoDeclarado));
    public Task<List<MovimientoCajaDto>> MovimientosAsync(int id) => http.GetAsync<List<MovimientoCajaDto>>($"caja/{id}/movimientos");
    public Task<MovimientoCajaDto> RegistrarMovimientoAsync(int id, MovimientoCajaCreateDto dto) =>
        http.PostAsync<MovimientoCajaDto>($"caja/{id}/movimientos", dto);
}

public sealed class VentasApi(ApiHttp http)
{
    public Task<VentaDto> CrearAsync(VentaCreateDto dto) => http.PostAsync<VentaDto>("ventas", dto);
    public Task<List<VentaResumenDto>> ListarAsync(int? sucursalId = null, int? cajaId = null, DateTime? desde = null, DateTime? hasta = null) =>
        http.GetAsync<List<VentaResumenDto>>(Query("ventas", ("sucursalId", sucursalId), ("cajaId", cajaId), ("desde", desde), ("hasta", hasta)));
    public Task<VentaDto> ObtenerAsync(int id) => http.GetAsync<VentaDto>($"ventas/{id}");
    public Task AnularAsync(int id, AnularVentaRequest request) => http.PostAsync($"ventas/{id}/anular", request);
}

public sealed class ComprasApi(ApiHttp http)
{
    public Task<List<CompraResumenDto>> ListarAsync(int? sucursalId = null) => http.GetAsync<List<CompraResumenDto>>(Query("compras", ("sucursalId", sucursalId)));
    public Task<CompraDto> ObtenerAsync(int id) => http.GetAsync<CompraDto>($"compras/{id}");
    public Task<int> CrearAsync(CompraCreateDto dto) => http.PostAsync<int>("compras", dto);
    public Task AnularAsync(int id) => http.PostAsync($"compras/{id}/anular", null);
}

public sealed class CreditosApi(ApiHttp http)
{
    public Task<List<CreditoResumenDto>> ListarAsync(int? clienteId = null, string? estado = null) =>
        http.GetAsync<List<CreditoResumenDto>>(Query("creditos", ("clienteId", clienteId), ("estado", estado)));
    public Task<CreditoDto> ObtenerAsync(int id) => http.GetAsync<CreditoDto>($"creditos/{id}");
    public Task<CreditoDto> AbonarAsync(int id, AbonoCreditoCreateDto dto) => http.PostAsync<CreditoDto>($"creditos/{id}/abonos", dto);
}

public sealed class UsuariosApi(ApiHttp http)
{
    public Task<List<UsuarioDto>> ListarAsync() => http.GetAsync<List<UsuarioDto>>("usuarios");
    public Task CrearAsync(UsuarioCreateDto dto) => http.PostAsync("usuarios", dto);
    public Task ActualizarAsync(int id, UsuarioUpdateDto dto) => http.PutAsync($"usuarios/{id}", dto);
    public Task CambiarEstadoAsync(int id, bool activo) => http.PutAsync(Query($"usuarios/{id}/estado", ("activo", activo)), null);
    public Task ResetPasswordAsync(int id, ResetPasswordRequest request) => http.PostAsync($"usuarios/{id}/reset-password", request);
    public Task DesbloquearAsync(int id) => http.PutAsync($"usuarios/{id}/desbloquear", null);
    public Task RevocarSesionesAsync(int id) => http.PostAsync($"usuarios/{id}/revocar-sesiones", null);
}

public sealed class RolesApi(ApiHttp http)
{
    public Task<List<RolDto>> ListarAsync() => http.GetAsync<List<RolDto>>("roles");
    public Task<RolDetalleDto> ObtenerAsync(int id) => http.GetAsync<RolDetalleDto>($"roles/{id}");
    public Task<int> CrearAsync(RolSaveDto dto) => http.PostAsync<int>("roles", dto);
    public Task ActualizarAsync(int id, RolSaveDto dto) => http.PutAsync($"roles/{id}", dto);
    public Task EliminarAsync(int id) => http.DeleteAsync($"roles/{id}");
    public Task<List<PermisoModuloDto>> CatalogoPermisosAsync() => http.GetAsync<List<PermisoModuloDto>>("permisos");
}

public sealed class AuditoriaApi(ApiHttp http)
{
    public Task<PaginaResultado<EventoSeguridadDto>> ListarAsync(DateTime? desde, DateTime? hasta, int? usuarioId, string? tipo, string? origen, int pagina, int tamanoPagina) =>
        http.GetAsync<PaginaResultado<EventoSeguridadDto>>(Query("auditoria",
            ("desde", desde), ("hasta", hasta), ("usuarioId", usuarioId), ("tipo", tipo), ("origen", origen),
            ("pagina", pagina), ("tamanoPagina", tamanoPagina)));
    public Task<List<string>> TiposAsync() => http.GetAsync<List<string>>("auditoria/tipos");
    public Task<AuditoriaEstadoDto> ObtenerEstadoAsync() => http.GetAsync<AuditoriaEstadoDto>("auditoria/estado");
    public Task ActualizarEstadoAsync(bool habilitada) => http.PutAsync("auditoria/estado", new AuditoriaEstadoDto(habilitada));

    /// <summary>Registra acciones de la UI (tipos permitidos: NAVEGACION_UI, CLIC_UI).</summary>
    public Task RegistrarClienteAsync(IReadOnlyList<EventoClienteDto> eventos) => http.PostAsync("auditoria/cliente", eventos);
}
