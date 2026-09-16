using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Enums;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class CompraService
{
    private readonly ICompraRepository _compraRepository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenant;

    public CompraService(
        ICompraRepository compraRepository,
        IInventarioRepository inventarioRepository,
        IProductoRepository productoRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenant)
    {
        _compraRepository = compraRepository;
        _inventarioRepository = inventarioRepository;
        _productoRepository = productoRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenant = tenant;
    }

    /// <summary>Registra una compra: ingresa stock (convertido a unidad base) en la sucursal activa del usuario.</summary>
    public async Task<int> CrearAsync(CompraCreateDto request)
    {
        if (request.Detalles.Count == 0)
            throw new ReglaDeNegocioException("La compra debe tener al menos un producto.");
        if (_tenant.SucursalId is null)
            throw new ReglaDeNegocioException("El usuario no tiene una sucursal activa asignada.");

        var sucursalId = _tenant.SucursalId.Value;
        // El impuesto de compra se deja en 0 en este esqueleto (ver decisión abierta #1 del plan de arquitectura).
        decimal subtotal = request.Detalles.Sum(d => d.CostoUnidadMedida * d.Cantidad);

        using var uow = _unitOfWorkFactory.Create();

        var compra = new Compra
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = sucursalId,
            ProveedorId = request.ProveedorId,
            UsuarioId = _tenant.UsuarioId,
            NumeroDocumentoProveedor = request.NumeroDocumentoProveedor,
            Fecha = DateTime.UtcNow,
            Subtotal = subtotal,
            ImpuestoTotal = 0,
            Total = subtotal,
            Estado = "COMPLETADA"
        };
        compra.Id = await _compraRepository.CrearAsync(compra, uow.Transaction);

        foreach (var d in request.Detalles)
        {
            var producto = await _productoRepository.ObtenerEntidadAsync(_tenant.EmpresaId, d.ProductoId)
                ?? throw new EntidadNoEncontradaException("Producto", d.ProductoId);
            // Presentación real del producto (mismo catálogo que usan las Ventas) resuelta en el
            // servidor — el cliente ya no puede mandar un factor de conversión inventado.
            var tipoPrecio = await _productoRepository.ObtenerTipoPrecioAsync(d.ProductoId, d.TipoPrecioId)
                ?? throw new EntidadNoEncontradaException("TipoPrecio", d.TipoPrecioId);

            var cantidadBase = d.Cantidad * tipoPrecio.CantidadBase;
            var inventario = await _inventarioRepository.ObtenerParaActualizarAsync(d.ProductoId, sucursalId, uow.Transaction);
            var stockActual = inventario?.StockActual ?? 0;
            var nuevoStock = stockActual + cantidadBase;

            if (inventario is null)
                await _inventarioRepository.CrearAsync(new Inventario
                {
                    ProductoId = d.ProductoId,
                    SucursalId = sucursalId,
                    StockActual = nuevoStock,
                    StockMinimo = 0,
                    FechaActualizacion = DateTime.UtcNow
                }, uow.Transaction);
            else
                await _inventarioRepository.ActualizarStockAsync(d.ProductoId, sucursalId, nuevoStock, uow.Transaction);

            await _inventarioRepository.RegistrarMovimientoAsync(new MovimientoInventario
            {
                EmpresaId = _tenant.EmpresaId,
                SucursalId = sucursalId,
                ProductoId = d.ProductoId,
                UsuarioId = _tenant.UsuarioId,
                TipoMovimiento = TipoMovimientoInventario.EntradaCompra.ToString(),
                Cantidad = cantidadBase,
                StockResultante = nuevoStock,
                DocumentoOrigenTipo = DocumentoOrigenTipo.Compra.ToString(),
                DocumentoOrigenId = compra.Id,
                FechaMovimiento = DateTime.UtcNow
            }, uow.Transaction);

            await _compraRepository.CrearDetalleAsync(new DetalleCompra
            {
                CompraId = compra.Id,
                ProductoId = d.ProductoId,
                TipoPrecioId = tipoPrecio.Id,
                Cantidad = d.Cantidad,
                CantidadBaseCalculada = cantidadBase,
                CostoUnitario = d.CostoUnidadMedida,
                Subtotal = d.CostoUnidadMedida * d.Cantidad
            }, uow.Transaction);

            // Deja el costo de referencia de esta presentación al día con el último precio pagado,
            // para que la siguiente compra (o el formulario de Productos) ya lo sugiera solo.
            await _productoRepository.ActualizarPrecioCompraAsync(tipoPrecio.Id, d.CostoUnidadMedida, uow.Transaction);
        }

        uow.Commit();
        return compra.Id;
    }

    public Task<IReadOnlyList<CompraResumenDto>> ListarAsync(int? sucursalId) => _compraRepository.ListarAsync(_tenant.EmpresaId, sucursalId);

    public async Task<CompraDto> ObtenerAsync(int id) =>
        await _compraRepository.ObtenerDetalleAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Compra", id);

    /// <summary>Anula una compra. La reversión de stock queda para una evolución futura (decisión abierta #8 del plan).</summary>
    public async Task AnularAsync(int id)
    {
        var compra = await _compraRepository.ObtenerEntidadAsync(_tenant.EmpresaId, id)
            ?? throw new EntidadNoEncontradaException("Compra", id);
        if (compra.Estado == "ANULADA")
            throw new ReglaDeNegocioException("La compra ya está anulada.");

        using var uow = _unitOfWorkFactory.Create();
        await _compraRepository.AnularAsync(id, uow.Transaction);
        uow.Commit();
    }
}
