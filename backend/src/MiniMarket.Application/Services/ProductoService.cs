using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class ProductoService
{
    private readonly IProductoRepository _repository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly ITenantContext _tenant;

    public ProductoService(IProductoRepository repository, IInventarioRepository inventarioRepository, ITenantContext tenant)
    {
        _repository = repository;
        _inventarioRepository = inventarioRepository;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<ProductoDto>> ListarAsync() => _repository.ListarAsync(_tenant.EmpresaId);

    public async Task<ProductoDto> ObtenerAsync(int id) =>
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Producto", id);

    /// <summary>Búsqueda rápida del POS por nombre o código de barras; requiere sucursal activa en el tenant.</summary>
    public Task<IReadOnlyList<ProductoPosDto>> BuscarParaPosAsync(string termino)
    {
        if (_tenant.SucursalId is null)
            throw new ReglaDeNegocioException("El usuario no tiene una sucursal activa asignada.");

        return _repository.BuscarParaPosAsync(_tenant.EmpresaId, _tenant.SucursalId.Value, termino);
    }

    public async Task<int> CrearAsync(ProductoCreateDto dto)
    {
        if (dto.TiposPrecio.Count == 0)
            throw new ReglaDeNegocioException("El producto debe tener al menos un tipo de precio.");
        if (dto.TiposPrecio.Count(t => t.EsDefault) != 1)
            throw new ReglaDeNegocioException("El producto debe tener exactamente un tipo de precio marcado como default.");

        var producto = new Producto
        {
            EmpresaId = _tenant.EmpresaId,
            CategoriaId = dto.CategoriaId,
            ProveedorId = dto.ProveedorId,
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            CodigoBarras = Normalizar(dto.CodigoBarras),
            CodigoInterno = Normalizar(dto.CodigoInterno),
            ImagenPath = dto.ImagenPath,
            UnidadBase = dto.UnidadBase,
            CreadoPorUsuarioId = _tenant.UsuarioId,
            FechaCreacion = DateTime.UtcNow
        };
        var productoId = await _repository.CrearAsync(producto);

        foreach (var tp in dto.TiposPrecio)
        {
            await _repository.CrearTipoPrecioAsync(new TipoPrecio
            {
                ProductoId = productoId,
                Nombre = tp.Nombre,
                CantidadBase = tp.CantidadBase,
                PrecioVenta = tp.PrecioVenta,
                PrecioCompra = tp.PrecioCompra,
                EsDefault = tp.EsDefault
            });
        }

        // Inicializa inventario en 0 para la sucursal activa (si el usuario que crea el producto tiene una asignada).
        if (_tenant.SucursalId is int sucursalId)
        {
            await _inventarioRepository.CrearAsync(new Inventario
            {
                ProductoId = productoId,
                SucursalId = sucursalId,
                StockActual = 0,
                StockMinimo = 0,
                FechaActualizacion = DateTime.UtcNow
            });
        }

        return productoId;
    }

    public async Task ActualizarAsync(int id, ProductoUpdateDto dto)
    {
        var producto = await _repository.ObtenerEntidadAsync(_tenant.EmpresaId, id)
            ?? throw new EntidadNoEncontradaException("Producto", id);

        producto.CategoriaId = dto.CategoriaId;
        producto.ProveedorId = dto.ProveedorId;
        producto.Nombre = dto.Nombre;
        producto.Descripcion = dto.Descripcion;
        producto.CodigoBarras = Normalizar(dto.CodigoBarras);
        producto.CodigoInterno = Normalizar(dto.CodigoInterno);
        producto.ImagenPath = dto.ImagenPath;
        producto.UnidadBase = dto.UnidadBase;
        producto.ModificadoPorUsuarioId = _tenant.UsuarioId;
        producto.FechaModificacion = DateTime.UtcNow;

        await _repository.ActualizarAsync(producto);
    }

    public Task DesactivarAsync(int id) => _repository.CambiarEstadoAsync(_tenant.EmpresaId, id, "I");

    public async Task<int> AgregarTipoPrecioAsync(int productoId, TipoPrecioCreateDto dto)
    {
        _ = await ObtenerAsync(productoId); // valida pertenencia a la empresa
        if (dto.EsDefault)
            await _repository.LimpiarDefaultAsync(productoId);

        return await _repository.CrearTipoPrecioAsync(new TipoPrecio
        {
            ProductoId = productoId,
            Nombre = dto.Nombre,
            CantidadBase = dto.CantidadBase,
            PrecioVenta = dto.PrecioVenta,
            PrecioCompra = dto.PrecioCompra,
            EsDefault = dto.EsDefault
        });
    }

    public async Task ActualizarTipoPrecioAsync(int productoId, int tipoPrecioId, TipoPrecioUpdateDto dto)
    {
        var tipoPrecio = await _repository.ObtenerTipoPrecioAsync(productoId, tipoPrecioId)
            ?? throw new EntidadNoEncontradaException("TipoPrecio", tipoPrecioId);

        if (dto.EsDefault && !tipoPrecio.EsDefault)
            await _repository.LimpiarDefaultAsync(productoId);

        tipoPrecio.Nombre = dto.Nombre;
        tipoPrecio.CantidadBase = dto.CantidadBase;
        tipoPrecio.PrecioVenta = dto.PrecioVenta;
        tipoPrecio.PrecioCompra = dto.PrecioCompra;
        tipoPrecio.EsDefault = dto.EsDefault;

        await _repository.ActualizarTipoPrecioAsync(tipoPrecio);
    }

    public Task EliminarTipoPrecioAsync(int productoId, int tipoPrecioId) =>
        _repository.EliminarTipoPrecioAsync(productoId, tipoPrecioId);

    /// <summary>
    /// Convierte "" o solo-espacios a null. Necesario porque el índice único
    /// UX_Producto_Empresa_CodigoBarras solo excluye NULL (no cadena vacía): sin esto, dos
    /// productos con el código de barras en blanco chocaban entre sí como si fueran duplicados.
    /// </summary>
    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
