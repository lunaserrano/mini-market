using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public CategoriaRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Categoria>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<Categoria>(
            "SELECT * FROM Categoria WHERE EmpresaId = @empresaId ORDER BY Nombre", new { empresaId });
        return items.AsList();
    }

    public async Task<Categoria?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Categoria>(
            "SELECT * FROM Categoria WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<int> CrearAsync(Categoria categoria)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Categoria (EmpresaId, Nombre, Descripcion, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @Nombre, @Descripcion, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """;
        return await connection.QuerySingleAsync<int>(sql, categoria);
    }

    public async Task ActualizarAsync(Categoria categoria)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Categoria SET Nombre = @Nombre, Descripcion = @Descripcion,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """;
        await connection.ExecuteAsync(sql, categoria);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("UPDATE Categoria SET Estado = @estado WHERE Id = @id AND EmpresaId = @empresaId", new { empresaId, id, estado });
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
            "SELECT * FROM Proveedor WHERE EmpresaId = @empresaId ORDER BY Nombre", new { empresaId });
        return items.AsList();
    }

    public async Task<Proveedor?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Proveedor>(
            "SELECT * FROM Proveedor WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<int> CrearAsync(Proveedor proveedor)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Proveedor (EmpresaId, Nombre, Contacto, Telefono, Email, Direccion, IdentificacionFiscal, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @Nombre, @Contacto, @Telefono, @Email, @Direccion, @IdentificacionFiscal, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """;
        return await connection.QuerySingleAsync<int>(sql, proveedor);
    }

    public async Task ActualizarAsync(Proveedor proveedor)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Proveedor SET Nombre = @Nombre, Contacto = @Contacto, Telefono = @Telefono, Email = @Email,
                Direccion = @Direccion, IdentificacionFiscal = @IdentificacionFiscal,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """;
        await connection.ExecuteAsync(sql, proveedor);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("UPDATE Proveedor SET Estado = @estado WHERE Id = @id AND EmpresaId = @empresaId", new { empresaId, id, estado });
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
            "SELECT * FROM Cliente WHERE EmpresaId = @empresaId ORDER BY Nombre", new { empresaId });
        return items.AsList();
    }

    public async Task<Cliente?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Cliente>(
            "SELECT * FROM Cliente WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<int> CrearAsync(Cliente cliente)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Cliente (EmpresaId, Nombre, IdentificacionFiscal, Telefono, Email, Direccion, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @Nombre, @IdentificacionFiscal, @Telefono, @Email, @Direccion, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """;
        return await connection.QuerySingleAsync<int>(sql, cliente);
    }

    public async Task ActualizarAsync(Cliente cliente)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Cliente SET Nombre = @Nombre, IdentificacionFiscal = @IdentificacionFiscal, Telefono = @Telefono,
                Email = @Email, Direccion = @Direccion,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """;
        await connection.ExecuteAsync(sql, cliente);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("UPDATE Cliente SET Estado = @estado WHERE Id = @id AND EmpresaId = @empresaId", new { empresaId, id, estado });
    }
}
