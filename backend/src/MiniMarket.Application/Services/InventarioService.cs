using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Enums;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class InventarioService
{
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IProductoRepository _productoRepository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenant;

    public InventarioService(
        IInventarioRepository inventarioRepository,
        IProductoRepository productoRepository,
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenant)
    {
        _inventarioRepository = inventarioRepository;
        _productoRepository = productoRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<InventarioDto>> ListarAsync(int? sucursalId, int? productoId) =>
        _inventarioRepository.ListarAsync(_tenant.EmpresaId, sucursalId, productoId);

    public async Task<InventarioDto> ObtenerAsync(int productoId, int sucursalId)
    {
        var producto = await _productoRepository.ObtenerEntidadAsync(_tenant.EmpresaId, productoId)
            ?? throw new EntidadNoEncontradaException("Producto", productoId);
        var inventario = await _inventarioRepository.ObtenerAsync(productoId, sucursalId)
            ?? new Inventario { ProductoId = productoId, SucursalId = sucursalId, StockActual = 0, StockMinimo = 0 };

        return new InventarioDto(productoId, producto.Nombre, sucursalId, inventario.StockActual, inventario.StockMinimo);
    }

    public Task<IReadOnlyList<MovimientoInventarioDto>> ListarMovimientosAsync(MovimientoInventarioFiltro filtro) =>
        _inventarioRepository.ListarMovimientosAsync(_tenant.EmpresaId, filtro);

    /// <summary>Ajuste manual de stock (+/-). Genera un MovimientoInventario con tipo AjustePositivo/AjusteNegativo.</summary>
    public async Task AjustarAsync(AjusteInventarioRequest request)
    {
        if (request.CantidadAjuste == 0)
            throw new ReglaDeNegocioException("La cantidad de ajuste no puede ser cero.");

        using var uow = _unitOfWorkFactory.Create();

        var inventario = await _inventarioRepository.ObtenerParaActualizarAsync(request.ProductoId, request.SucursalId, uow.Transaction);
        var stockActual = inventario?.StockActual ?? 0;
        var nuevoStock = stockActual + request.CantidadAjuste;

        if (nuevoStock < 0)
            throw new StockInsuficienteException($"producto #{request.ProductoId}", stockActual, -request.CantidadAjuste);

        if (inventario is null)
            await _inventarioRepository.CrearAsync(new Inventario
            {
                ProductoId = request.ProductoId,
                SucursalId = request.SucursalId,
                StockActual = nuevoStock,
                StockMinimo = 0,
                FechaActualizacion = DateTime.UtcNow
            }, uow.Transaction);
        else
            await _inventarioRepository.ActualizarStockAsync(request.ProductoId, request.SucursalId, nuevoStock, uow.Transaction);

        await _inventarioRepository.RegistrarMovimientoAsync(new MovimientoInventario
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = request.SucursalId,
            ProductoId = request.ProductoId,
            UsuarioId = _tenant.UsuarioId,
            TipoMovimiento = (request.CantidadAjuste > 0 ? TipoMovimientoInventario.AjustePositivo : TipoMovimientoInventario.AjusteNegativo).ToString(),
            Cantidad = request.CantidadAjuste,
            StockResultante = nuevoStock,
            DocumentoOrigenTipo = DocumentoOrigenTipo.AjusteManual.ToString(),
            Observacion = request.Observacion,
            FechaMovimiento = DateTime.UtcNow
        }, uow.Transaction);

        uow.Commit();
    }
}
