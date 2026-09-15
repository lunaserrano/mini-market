using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Enums;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

/// <summary>
/// Orquesta el flujo de venta del POS. Es la pieza más sensible del sistema: sin EF Core no hay
/// SaveChanges transaccional automático, así que TODA la operación corre dentro de una única
/// transacción ADO.NET (<see cref="IUnitOfWork"/>) que se hace commit solo al final; cualquier
/// excepción hace rollback implícito al hacer Dispose sin haber llamado Commit().
/// </summary>
public class VentaService
{
    private readonly IVentaRepository _ventaRepository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly ICajaRepository _cajaRepository;
    private readonly IEmpresaRepository _empresaRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenant;

    public VentaService(
        IVentaRepository ventaRepository,
        IInventarioRepository inventarioRepository,
        IProductoRepository productoRepository,
        ICajaRepository cajaRepository,
        IEmpresaRepository empresaRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenant)
    {
        _ventaRepository = ventaRepository;
        _inventarioRepository = inventarioRepository;
        _productoRepository = productoRepository;
        _cajaRepository = cajaRepository;
        _empresaRepository = empresaRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenant = tenant;
    }

    public async Task<VentaDto> CrearAsync(VentaCreateDto request)
    {
        if (request.Detalles.Count == 0)
            throw new ReglaDeNegocioException("La venta debe tener al menos un producto.");

        var caja = await _cajaRepository.ObtenerAbiertaPorUsuarioAsync(_tenant.EmpresaId, _tenant.UsuarioId)
            ?? throw new CajaCerradaException("Debe abrir una caja antes de registrar ventas.");

        var sucursalId = _tenant.SucursalId ?? caja.SucursalId;

        var empresa = await _empresaRepository.ObtenerPorIdAsync(_tenant.EmpresaId)
            ?? throw new EntidadNoEncontradaException("Empresa", _tenant.EmpresaId);
        var factorImpuesto = 1 + empresa.TasaImpuesto / 100m;

        // --- Paso 1: resolver producto + tipo de precio de cada línea y calcular totales (solo lectura) ---
        // TipoPrecio.PrecioVenta es el precio FINAL con IVA incluido (lo que se cobra tal cual en el
        // POS) — el IVA se desglosa "hacia adentro" con la tasa única de la empresa, en vez de sumarse
        // encima. Así el total que ve/valida el cajero en pantalla es exactamente el mismo que calcula
        // el backend, sin sorpresas al cobrar.
        var lineas = new List<(Producto Producto, TipoPrecio TipoPrecio, DetalleVentaCreateDto Origen, decimal CantidadBase, decimal LineConIva, decimal LineBase, decimal LineImpuesto)>();
        decimal subtotal = 0, descuentoTotal = 0, impuestoTotal = 0, total = 0;

        foreach (var d in request.Detalles)
        {
            var producto = await _productoRepository.ObtenerEntidadAsync(_tenant.EmpresaId, d.ProductoId)
                ?? throw new EntidadNoEncontradaException("Producto", d.ProductoId);
            var tipoPrecio = await _productoRepository.ObtenerTipoPrecioAsync(d.ProductoId, d.TipoPrecioId)
                ?? throw new EntidadNoEncontradaException("TipoPrecio", d.TipoPrecioId);

            var cantidadBase = d.Cantidad * tipoPrecio.CantidadBase;
            var lineConIva = tipoPrecio.PrecioVenta * d.Cantidad - d.Descuento; // lo que se cobra por esta línea, IVA incluido
            var lineBase = Math.Round(lineConIva / factorImpuesto, 2);
            var lineImpuesto = lineConIva - lineBase;

            lineas.Add((producto, tipoPrecio, d, cantidadBase, lineConIva, lineBase, lineImpuesto));
            subtotal += lineBase;
            descuentoTotal += d.Descuento;
            impuestoTotal += lineImpuesto;
            total += lineConIva;
        }

        var totalPagado = request.Pagos.Sum(p => p.Monto);
        if (totalPagado + 0.01m < total)
            throw new PagosInsuficientesException(total, totalPagado);

        // --- Paso 2: transacción — folio, descuento de stock, inserciones ---
        using var uow = _unitOfWorkFactory.Create();

        var folio = await _ventaRepository.ObtenerSiguienteFolioAsync(sucursalId, uow.Transaction);

        var venta = new Venta
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = sucursalId,
            CajaId = caja.Id,
            ClienteId = request.ClienteId,
            UsuarioId = _tenant.UsuarioId,
            Folio = folio,
            Fecha = DateTime.UtcNow,
            Subtotal = subtotal,
            DescuentoTotal = descuentoTotal,
            ImpuestoTotal = impuestoTotal,
            Total = total,
            Estado = "COMPLETADA"
        };
        venta.Id = await _ventaRepository.CrearAsync(venta, uow.Transaction);

        var detallesDto = new List<DetalleVentaDto>();
        foreach (var (producto, tipoPrecio, origen, cantidadBase, lineConIva, _, _) in lineas)
        {
            var inventario = await _inventarioRepository.ObtenerParaActualizarAsync(producto.Id, sucursalId, uow.Transaction);
            var stockActual = inventario?.StockActual ?? 0;
            if (stockActual < cantidadBase)
                throw new StockInsuficienteException(producto.Nombre, stockActual, cantidadBase);

            var nuevoStock = stockActual - cantidadBase;
            await _inventarioRepository.ActualizarStockAsync(producto.Id, sucursalId, nuevoStock, uow.Transaction);

            await _inventarioRepository.RegistrarMovimientoAsync(new MovimientoInventario
            {
                EmpresaId = _tenant.EmpresaId,
                SucursalId = sucursalId,
                ProductoId = producto.Id,
                UsuarioId = _tenant.UsuarioId,
                TipoMovimiento = TipoMovimientoInventario.SalidaVenta.ToString(),
                Cantidad = -cantidadBase,
                StockResultante = nuevoStock,
                DocumentoOrigenTipo = DocumentoOrigenTipo.Venta.ToString(),
                DocumentoOrigenId = venta.Id,
                FechaMovimiento = DateTime.UtcNow
            }, uow.Transaction);

            var detalle = new DetalleVenta
            {
                VentaId = venta.Id,
                ProductoId = producto.Id,
                TipoPrecioId = tipoPrecio.Id,
                Cantidad = origen.Cantidad,
                CantidadBaseCalculada = cantidadBase,
                PrecioUnitario = tipoPrecio.PrecioVenta,
                Descuento = origen.Descuento,
                Subtotal = lineConIva
            };
            await _ventaRepository.CrearDetalleAsync(detalle, uow.Transaction);
            detallesDto.Add(new DetalleVentaDto(producto.Id, producto.Nombre, tipoPrecio.Id, tipoPrecio.Nombre,
                origen.Cantidad, cantidadBase, tipoPrecio.PrecioVenta, origen.Descuento, lineConIva));
        }

        var pagosDto = new List<PagoVentaDto>();
        foreach (var p in request.Pagos)
        {
            await _ventaRepository.CrearPagoAsync(new PagoVenta
            {
                VentaId = venta.Id,
                Metodo = p.Metodo.ToUpperInvariant(),
                Monto = p.Monto,
                Referencia = p.Referencia,
                Fecha = DateTime.UtcNow
            }, uow.Transaction);
            pagosDto.Add(new PagoVentaDto(p.Metodo.ToUpperInvariant(), p.Monto, p.Referencia));
        }

        // Si hay pago en efectivo, se refleja como ingreso de caja para que el corte de turno cuadre.
        var montoEfectivo = request.Pagos.Where(p => p.Metodo.Equals("EFECTIVO", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Monto);
        if (montoEfectivo > 0)
        {
            await _cajaRepository.RegistrarMovimientoAsync(new MovimientoCaja
            {
                CajaId = caja.Id,
                Tipo = "INGRESO",
                Concepto = $"Venta #{folio}",
                Monto = montoEfectivo,
                UsuarioId = _tenant.UsuarioId,
                Fecha = DateTime.UtcNow,
                DocumentoReferenciaTipo = DocumentoOrigenTipo.Venta.ToString(),
                DocumentoReferenciaId = venta.Id
            }, uow.Transaction);
        }

        uow.Commit();

        return new VentaDto(venta.Id, folio, venta.Fecha, venta.ClienteId, venta.Estado,
            subtotal, descuentoTotal, impuestoTotal, total, detallesDto, pagosDto);
    }

    public Task<IReadOnlyList<VentaResumenDto>> ListarAsync(int? sucursalId, DateTime? desde, DateTime? hasta)
    {
        // Un cajero solo ve sus propias ventas; admin/supervisor ven todas las de la sucursal/empresa.
        var usuarioId = _tenant.Rol.Equals("cajero", StringComparison.OrdinalIgnoreCase) ? _tenant.UsuarioId : (int?)null;
        return _ventaRepository.ListarAsync(_tenant.EmpresaId, sucursalId, usuarioId, desde, hasta);
    }

    public async Task<VentaDto> ObtenerAsync(int id) =>
        await _ventaRepository.ObtenerDetalleAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Venta", id);

    public async Task AnularAsync(int id, AnularVentaRequest request)
    {
        var venta = await _ventaRepository.ObtenerEntidadAsync(_tenant.EmpresaId, id)
            ?? throw new EntidadNoEncontradaException("Venta", id);
        if (venta.Estado == "ANULADA")
            throw new ReglaDeNegocioException("La venta ya está anulada.");

        using var uow = _unitOfWorkFactory.Create();
        await _ventaRepository.AnularAsync(id, _tenant.UsuarioId, request.Motivo, uow.Transaction);
        // Nota: revertir stock de una venta anulada es responsabilidad de esta misma transacción en una
        // evolución futura (leer DetalleVenta y aplicar MovimientoInventario DevolucionVenta por línea);
        // se deja fuera del alcance de este esqueleto (ver decisión abierta #8 del plan de arquitectura).
        uow.Commit();
    }
}
