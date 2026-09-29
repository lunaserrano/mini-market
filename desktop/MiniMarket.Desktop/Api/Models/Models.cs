// Contratos HTTP de la Api (espejo de backend/src/MiniMarket.Application/DTOs/*.cs).
// Se duplican a propósito en vez de usar los de MiniMarket.Application: aunque la Api corre en este
// mismo proceso (Services/ApiLocal.cs), las pantallas solo conocen el contrato JSON, no el backend.
// Este namespace se EXCLUYE de la ofuscación (obfuscar.xml): System.Text.Json los mapea por nombre.

namespace MiniMarket.Desktop.Api.Models;

// ---------- Auth ----------
public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);
public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public record LoginResponse(string Token, DateTime ExpiraUtc, string RefreshToken, DateTime RefreshExpiraUtc, UsuarioActualDto Usuario);

public record UsuarioActualDto(
    int Id, int EmpresaId, int? SucursalId, string NombreCompleto, string Username,
    string Rol, string RolNombre, IReadOnlyList<string> Permisos, bool DebeCambiarPassword);

public record HealthDto(string Status, bool Db, string? Version, string? Modo, DateTime FechaUtc);

// ---------- Empresa ----------
public record EmpresaDto(int Id, string Nombre, string? RazonSocial, string? IdentificacionFiscal,
    string ZonaHoraria, string CodigoMoneda, string SimboloMoneda, decimal TasaImpuesto);

public record EmpresaUpdateDto(string Nombre, string? RazonSocial, string? IdentificacionFiscal,
    string ZonaHoraria, string CodigoMoneda, string SimboloMoneda, decimal TasaImpuesto);

// ---------- Catálogos ----------
public record CategoriaDto(int Id, string Nombre, string? Descripcion, string Estado);
public record CategoriaSaveDto(string Nombre, string? Descripcion);

public record ProveedorDto(int Id, string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, string? IdentificacionFiscal, string Estado);
public record ProveedorSaveDto(string Nombre, string? Contacto, string? Telefono, string? Email, string? Direccion, string? IdentificacionFiscal);

public record ClienteDto(int Id, string Nombre, string? IdentificacionFiscal, string? Telefono, string? Email, string? Direccion, string Estado);
public record ClienteSaveDto(string Nombre, string? IdentificacionFiscal, string? Telefono, string? Email, string? Direccion);

// ---------- Productos ----------
public record TipoPrecioDto(int Id, string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault, string Estado);
public record TipoPrecioSaveDto(string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault);

public record ProductoDto(
    int Id, int CategoriaId, string? CategoriaNombre, int? ProveedorId, string Nombre, string? Descripcion,
    string? CodigoBarras, string? CodigoInterno, string? ImagenPath, string UnidadBase, string Estado,
    IReadOnlyList<TipoPrecioDto> TiposPrecio)
{
    public decimal? PrecioDefault => (TiposPrecio.FirstOrDefault(t => t.EsDefault) ?? TiposPrecio.FirstOrDefault())?.PrecioVenta;
}

public record ProductoCreateDto(int CategoriaId, int? ProveedorId, string Nombre, string? Descripcion, string? CodigoBarras,
    string? CodigoInterno, string? ImagenPath, string UnidadBase, IReadOnlyList<TipoPrecioSaveDto> TiposPrecio);

public record ProductoUpdateDto(int CategoriaId, int? ProveedorId, string Nombre, string? Descripcion, string? CodigoBarras,
    string? CodigoInterno, string? ImagenPath, string UnidadBase);

public record ProductoPosDto(int ProductoId, string Nombre, string? CodigoBarras, string UnidadBase, decimal StockActual, IReadOnlyList<TipoPrecioDto> TiposPrecio);

// ---------- Inventario ----------
public record InventarioDto(int ProductoId, string ProductoNombre, int SucursalId, decimal StockActual, decimal StockMinimo)
{
    public bool BajoMinimo => StockActual <= StockMinimo;
}

public record AjusteInventarioRequest(int ProductoId, int SucursalId, decimal CantidadAjuste, string Observacion);

public record MovimientoInventarioDto(int Id, int ProductoId, string ProductoNombre, int SucursalId, string TipoMovimiento,
    decimal Cantidad, decimal StockResultante, string? DocumentoOrigenTipo, int? DocumentoOrigenId, string? Observacion, DateTime FechaMovimiento);

// ---------- Caja ----------
public record CajaDto(int Id, int SucursalId, int UsuarioAperturaId, string UsuarioAperturaNombre, DateTime FechaApertura, decimal MontoInicial,
    int? UsuarioCierreId, string? UsuarioCierreNombre, DateTime? FechaCierre, decimal? MontoFinalDeclarado, decimal? MontoFinalSistema,
    decimal? Diferencia, string Estado);

public record AperturaCajaRequest(decimal MontoInicial);
public record CierreCajaRequest(decimal MontoFinalDeclarado);
public record MovimientoCajaDto(int Id, int CajaId, string Tipo, string Concepto, decimal Monto, DateTime Fecha);
public record MovimientoCajaCreateDto(string Tipo, string Concepto, decimal Monto);

// ---------- Ventas ----------
public record DetalleVentaCreateDto(int ProductoId, int TipoPrecioId, decimal Cantidad, decimal Descuento);
public record PagoVentaCreateDto(string Metodo, decimal Monto, string? Referencia);
public record VentaCreateDto(int? ClienteId, IReadOnlyList<DetalleVentaCreateDto> Detalles, IReadOnlyList<PagoVentaCreateDto> Pagos,
    bool AlCredito = false, DateTime? FechaVencimiento = null);

public record DetalleVentaDto(int ProductoId, string ProductoNombre, int TipoPrecioId, string TipoPrecioNombre,
    decimal Cantidad, decimal CantidadBaseCalculada, decimal PrecioUnitario, decimal Descuento, decimal Subtotal);
public record PagoVentaDto(string Metodo, decimal Monto, string? Referencia);
public record VentaDto(int Id, int Folio, DateTime Fecha, int? ClienteId, string Estado,
    decimal Subtotal, decimal DescuentoTotal, decimal ImpuestoTotal, decimal Total,
    IReadOnlyList<DetalleVentaDto> Detalles, IReadOnlyList<PagoVentaDto> Pagos);
public record VentaResumenDto(int Id, int Folio, DateTime Fecha, string? ClienteNombre, decimal Total, string Estado, decimal? SaldoCredito);
public record AnularVentaRequest(string Motivo, bool RestituirStock = true);

// ---------- Compras ----------
public record DetalleCompraCreateDto(int ProductoId, int TipoPrecioId, decimal Cantidad, decimal CostoUnidadMedida);
public record CompraCreateDto(int ProveedorId, string? NumeroDocumentoProveedor, IReadOnlyList<DetalleCompraCreateDto> Detalles);
public record DetalleCompraDto(int ProductoId, string ProductoNombre, int? TipoPrecioId, string? TipoPrecioNombre,
    decimal Cantidad, decimal CantidadBaseCalculada, decimal CostoUnitario, decimal Subtotal);
public record CompraDto(int Id, DateTime Fecha, int ProveedorId, string ProveedorNombre, string? NumeroDocumentoProveedor,
    decimal Subtotal, decimal ImpuestoTotal, decimal Total, string Estado, IReadOnlyList<DetalleCompraDto> Detalles);
public record CompraResumenDto(int Id, DateTime Fecha, string ProveedorNombre, decimal Total, string Estado);

// ---------- Créditos ----------
public record AbonoCreditoCreateDto(string Metodo, decimal Monto, string? Referencia);
public record AbonoCreditoDto(int Id, DateTime Fecha, string Metodo, decimal Monto, string? Referencia, string UsuarioNombre);
public record CreditoResumenDto(int Id, int VentaId, int VentaFolio, int ClienteId, string ClienteNombre,
    DateTime FechaCreacion, DateTime? FechaVencimiento, decimal MontoOriginal, decimal SaldoPendiente, string Estado, bool Vencido);
public record CreditoDto(int Id, int VentaId, int VentaFolio, int ClienteId, string ClienteNombre,
    DateTime FechaCreacion, DateTime? FechaVencimiento, DateTime? FechaCancelacion,
    decimal TotalVenta, decimal MontoOriginal, decimal SaldoPendiente, string Estado, bool Vencido,
    IReadOnlyList<AbonoCreditoDto> Abonos);

// ---------- Usuarios / Roles / Auditoría ----------
public record UsuarioDto(int Id, int? SucursalId, string NombreCompleto, string Username, int RolId, string Rol, string RolNombre,
    string Estado, bool Bloqueado, DateTime? BloqueadoHasta, bool DebeCambiarPassword, DateTime? UltimoLoginUtc);
public record UsuarioCreateDto(int? SucursalId, string NombreCompleto, string Username, string Password, int RolId, bool DebeCambiarPassword = true);
public record UsuarioUpdateDto(int? SucursalId, string NombreCompleto, int RolId);
public record ResetPasswordRequest(string NuevaPassword, bool DebeCambiarPassword = true);

public record RolDto(int Id, string Codigo, string Nombre, string? Descripcion, bool EsSistema, int TotalUsuarios, int TotalPermisos);
public record RolDetalleDto(int Id, string Codigo, string Nombre, string? Descripcion, bool EsSistema, int TotalUsuarios, IReadOnlyList<string> Permisos);
public record RolSaveDto(string Nombre, string? Descripcion, IReadOnlyList<string> Permisos);
public record PermisoDto(string Codigo, string Modulo, string Nombre);
public record PermisoModuloDto(string Modulo, IReadOnlyList<PermisoDto> Permisos);

public record EventoSeguridadDto(long Id, DateTime FechaUtc, string Tipo, string? Detalle, int? ActorUsuarioId, string? ActorNombre,
    int? UsuarioObjetivoId, string? ObjetivoNombre, string? Ip, string Origen, string? Metodo, string? Ruta,
    short? StatusCode, int? DuracionMs, string? Datos);
public record EventoClienteDto(string Tipo, DateTime? FechaUtc, string? Ruta, string? Detalle, Dictionary<string, string?>? Datos);
public record PaginaResultado<T>(IReadOnlyList<T> Items, int Total, int Pagina, int TamanoPagina);
public record AuditoriaEstadoDto(bool Habilitada);
