using System.Data;
using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class CajaRepository : ICajaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CajaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Caja?> ObtenerAbiertaPorUsuarioAsync(int empresaId, int usuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Caja>(
            "SELECT * FROM Caja WHERE EmpresaId = @empresaId AND UsuarioAperturaId = @usuarioId AND Estado = 'ABIERTA'",
            new { empresaId, usuarioId });
    }

    public async Task<Caja?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Caja>(
            "SELECT * FROM Caja WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<IReadOnlyList<Caja>> ListarAsync(int empresaId, int? sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            SELECT * FROM Caja WHERE EmpresaId = @empresaId AND (@sucursalId IS NULL OR SucursalId = @sucursalId)
            ORDER BY FechaApertura DESC
            """;
        var items = await connection.QueryAsync<Caja>(sql, new { empresaId, sucursalId });
        return items.AsList();
    }

    public async Task<int> AbrirAsync(Caja caja)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Caja (EmpresaId, SucursalId, UsuarioAperturaId, FechaApertura, MontoInicial, Estado)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @UsuarioAperturaId, @FechaApertura, @MontoInicial, @Estado)
            """;
        return await connection.QuerySingleAsync<int>(sql, caja);
    }

    public async Task CerrarAsync(Caja caja)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Caja SET UsuarioCierreId = @UsuarioCierreId, FechaCierre = @FechaCierre,
                MontoFinalDeclarado = @MontoFinalDeclarado, MontoFinalSistema = @MontoFinalSistema,
                Diferencia = @Diferencia, Estado = @Estado
            WHERE Id = @Id
            """;
        await connection.ExecuteAsync(sql, caja);
    }

    public async Task<int> RegistrarMovimientoAsync(MovimientoCaja movimiento, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            const string sql = """
                INSERT INTO MovimientoCaja (CajaId, Tipo, Concepto, Monto, UsuarioId, Fecha, DocumentoReferenciaTipo, DocumentoReferenciaId)
                OUTPUT INSERTED.Id
                VALUES (@CajaId, @Tipo, @Concepto, @Monto, @UsuarioId, @Fecha, @DocumentoReferenciaTipo, @DocumentoReferenciaId)
                """;
            return await connection.QuerySingleAsync<int>(sql, movimiento, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task<IReadOnlyList<MovimientoCaja>> ListarMovimientosAsync(int cajaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<MovimientoCaja>(
            "SELECT * FROM MovimientoCaja WHERE CajaId = @cajaId ORDER BY Fecha", new { cajaId });
        return items.AsList();
    }

    public async Task<(decimal Ingresos, decimal Egresos)> ObtenerTotalesMovimientosAsync(int cajaId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            const string sql = """
                SELECT
                    ISNULL(SUM(CASE WHEN Tipo = 'INGRESO' THEN Monto ELSE 0 END), 0) AS Ingresos,
                    ISNULL(SUM(CASE WHEN Tipo = 'EGRESO' THEN Monto ELSE 0 END), 0) AS Egresos
                FROM MovimientoCaja WHERE CajaId = @cajaId
                """;
            return await connection.QuerySingleAsync<(decimal Ingresos, decimal Egresos)>(sql, new { cajaId }, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }
}
