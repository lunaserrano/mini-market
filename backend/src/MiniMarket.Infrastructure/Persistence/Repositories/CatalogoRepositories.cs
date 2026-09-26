using System.Data;
using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

// NOTA IMPORTANTE sobre CommandType.StoredProcedure: a diferencia de CommandType.Text (donde un
// parámetro de más simplemente no se usa), SQL Server rechaza con el error 8144 "has too many
// arguments specified" si se envía un parámetro que no existe en la firma del procedure. Por eso
// aquí nunca se pasa una entidad completa como parámetros (traería columnas como Id que el
// procedure de creación no declara): siempre se arma un objeto anónimo con exactamente los
// parámetros que el procedure espera.

public class CategoriaRepository : ICategoriaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public CategoriaRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Categoria>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<Categoria>(
            "market.usp_Categoria_Listar", new { empresaId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<Categoria?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Categoria>(
            "market.usp_Categoria_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(Categoria categoria)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Categoria_Crear", new
        {
            categoria.EmpresaId, categoria.Nombre, categoria.Descripcion, categoria.Estado,
            categoria.CreadoPorUsuarioId, categoria.FechaCreacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Categoria categoria)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Categoria_Actualizar", new
        {
            categoria.Id, categoria.EmpresaId, categoria.Nombre, categoria.Descripcion,
            categoria.ModificadoPorUsuarioId, categoria.FechaModificacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Categoria_CambiarEstado", new { empresaId, id, estado }, commandType: CommandType.StoredProcedure);
    }
}

public class ProveedorRepository : IProveedorRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public ProveedorRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Proveedor>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<Proveedor>(
            "market.usp_Proveedor_Listar", new { empresaId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<Proveedor?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Proveedor>(
            "market.usp_Proveedor_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(Proveedor proveedor)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Proveedor_Crear", new
        {
            proveedor.EmpresaId, proveedor.Nombre, proveedor.Contacto, proveedor.Telefono, proveedor.Email,
            proveedor.Direccion, proveedor.IdentificacionFiscal, proveedor.Estado,
            proveedor.CreadoPorUsuarioId, proveedor.FechaCreacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Proveedor proveedor)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Proveedor_Actualizar", new
        {
            proveedor.Id, proveedor.EmpresaId, proveedor.Nombre, proveedor.Contacto, proveedor.Telefono,
            proveedor.Email, proveedor.Direccion, proveedor.IdentificacionFiscal,
            proveedor.ModificadoPorUsuarioId, proveedor.FechaModificacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Proveedor_CambiarEstado", new { empresaId, id, estado }, commandType: CommandType.StoredProcedure);
    }
}

public class ClienteRepository : IClienteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public ClienteRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Cliente>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<Cliente>(
            "market.usp_Cliente_Listar", new { empresaId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<Cliente?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Cliente>(
            "market.usp_Cliente_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(Cliente cliente)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Cliente_Crear", new
        {
            cliente.EmpresaId, cliente.Nombre, cliente.IdentificacionFiscal, cliente.Telefono, cliente.Email,
            cliente.Direccion, cliente.Estado, cliente.CreadoPorUsuarioId, cliente.FechaCreacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Cliente cliente)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Cliente_Actualizar", new
        {
            cliente.Id, cliente.EmpresaId, cliente.Nombre, cliente.IdentificacionFiscal, cliente.Telefono,
            cliente.Email, cliente.Direccion, cliente.ModificadoPorUsuarioId, cliente.FechaModificacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Cliente_CambiarEstado", new { empresaId, id, estado }, commandType: CommandType.StoredProcedure);
    }
}
