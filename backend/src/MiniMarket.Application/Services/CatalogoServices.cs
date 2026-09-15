using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class CategoriaService
{
    private readonly ICategoriaRepository _repository;
    private readonly ITenantContext _tenant;

    public CategoriaService(ICategoriaRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    private static CategoriaDto Mapear(Categoria c) => new(c.Id, c.Nombre, c.Descripcion, c.Estado);

    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync() => (await _repository.ListarAsync(_tenant.EmpresaId)).Select(Mapear).ToList();

    public async Task<CategoriaDto> ObtenerAsync(int id) => Mapear(
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Categoria", id));

    private async Task<Categoria> ObtenerEntidadAsync(int id) =>
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Categoria", id);

    public Task<int> CrearAsync(CategoriaCreateDto dto) => _repository.CrearAsync(new Categoria
    {
        EmpresaId = _tenant.EmpresaId,
        Nombre = dto.Nombre,
        Descripcion = dto.Descripcion,
        CreadoPorUsuarioId = _tenant.UsuarioId,
        FechaCreacion = DateTime.UtcNow
    });

    public async Task ActualizarAsync(int id, CategoriaUpdateDto dto)
    {
        var categoria = await ObtenerEntidadAsync(id);
        categoria.Nombre = dto.Nombre;
        categoria.Descripcion = dto.Descripcion;
        categoria.ModificadoPorUsuarioId = _tenant.UsuarioId;
        categoria.FechaModificacion = DateTime.UtcNow;
        await _repository.ActualizarAsync(categoria);
    }

    public Task DesactivarAsync(int id) => _repository.CambiarEstadoAsync(_tenant.EmpresaId, id, "I");
}

public class ProveedorService
{
    private readonly IProveedorRepository _repository;
    private readonly ITenantContext _tenant;

    public ProveedorService(IProveedorRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    private static ProveedorDto Mapear(Proveedor p) => new(p.Id, p.Nombre, p.Contacto, p.Telefono, p.Email, p.Direccion, p.IdentificacionFiscal, p.Estado);

    public async Task<IReadOnlyList<ProveedorDto>> ListarAsync() => (await _repository.ListarAsync(_tenant.EmpresaId)).Select(Mapear).ToList();

    public async Task<ProveedorDto> ObtenerAsync(int id) => Mapear(
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Proveedor", id));

    private async Task<Proveedor> ObtenerEntidadAsync(int id) =>
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Proveedor", id);

    public Task<int> CrearAsync(ProveedorCreateDto dto) => _repository.CrearAsync(new Proveedor
    {
        EmpresaId = _tenant.EmpresaId,
        Nombre = dto.Nombre,
        Contacto = dto.Contacto,
        Telefono = dto.Telefono,
        Email = dto.Email,
        Direccion = dto.Direccion,
        IdentificacionFiscal = dto.IdentificacionFiscal,
        CreadoPorUsuarioId = _tenant.UsuarioId,
        FechaCreacion = DateTime.UtcNow
    });

    public async Task ActualizarAsync(int id, ProveedorUpdateDto dto)
    {
        var proveedor = await ObtenerEntidadAsync(id);
        proveedor.Nombre = dto.Nombre;
        proveedor.Contacto = dto.Contacto;
        proveedor.Telefono = dto.Telefono;
        proveedor.Email = dto.Email;
        proveedor.Direccion = dto.Direccion;
        proveedor.IdentificacionFiscal = dto.IdentificacionFiscal;
        proveedor.ModificadoPorUsuarioId = _tenant.UsuarioId;
        proveedor.FechaModificacion = DateTime.UtcNow;
        await _repository.ActualizarAsync(proveedor);
    }

    public Task DesactivarAsync(int id) => _repository.CambiarEstadoAsync(_tenant.EmpresaId, id, "I");
}

public class ClienteService
{
    private readonly IClienteRepository _repository;
    private readonly ITenantContext _tenant;

    public ClienteService(IClienteRepository repository, ITenantContext tenant)
    {
        _repository = repository;
        _tenant = tenant;
    }

    private static ClienteDto Mapear(Cliente c) => new(c.Id, c.Nombre, c.IdentificacionFiscal, c.Telefono, c.Email, c.Direccion, c.Estado);

    public async Task<IReadOnlyList<ClienteDto>> ListarAsync() => (await _repository.ListarAsync(_tenant.EmpresaId)).Select(Mapear).ToList();

    public async Task<ClienteDto> ObtenerAsync(int id) => Mapear(
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Cliente", id));

    private async Task<Cliente> ObtenerEntidadAsync(int id) =>
        await _repository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Cliente", id);

    public Task<int> CrearAsync(ClienteCreateDto dto) => _repository.CrearAsync(new Cliente
    {
        EmpresaId = _tenant.EmpresaId,
        Nombre = dto.Nombre,
        IdentificacionFiscal = dto.IdentificacionFiscal,
        Telefono = dto.Telefono,
        Email = dto.Email,
        Direccion = dto.Direccion,
        CreadoPorUsuarioId = _tenant.UsuarioId,
        FechaCreacion = DateTime.UtcNow
    });

    public async Task ActualizarAsync(int id, ClienteUpdateDto dto)
    {
        var cliente = await ObtenerEntidadAsync(id);
        cliente.Nombre = dto.Nombre;
        cliente.IdentificacionFiscal = dto.IdentificacionFiscal;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.Direccion = dto.Direccion;
        cliente.ModificadoPorUsuarioId = _tenant.UsuarioId;
        cliente.FechaModificacion = DateTime.UtcNow;
        await _repository.ActualizarAsync(cliente);
    }

    public Task DesactivarAsync(int id) => _repository.CambiarEstadoAsync(_tenant.EmpresaId, id, "I");
}
