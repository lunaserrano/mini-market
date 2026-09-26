using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
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
            "market.usp_Caja_ObtenerAbiertaPorUsuario", new { empresaId, usuarioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<Caja?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Caja>(
            "market.usp_Caja_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<CajaDto>> ListarAsync(int empresaId, int? sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<CajaDto>(
            "market.usp_Caja_Listar", new { empresaId, sucursalId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<int> AbrirAsync(Caja caja)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Caja_Abrir", new
        {
            caja.EmpresaId, caja.SucursalId, caja.UsuarioAperturaId, caja.FechaApertura, caja.MontoInicial, caja.Estado
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CerrarAsync(Caja caja)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Caja_Cerrar", new
        {
            caja.Id, caja.UsuarioCierreId, caja.FechaCierre, caja.MontoFinalDeclarado,
            caja.MontoFinalSistema, caja.Diferencia, caja.Estado
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> RegistrarMovimientoAsync(MovimientoCaja movimiento, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleAsync<int>("market.usp_MovimientoCaja_Registrar", new
            {
                movimiento.CajaId, movimiento.Tipo, movimiento.Concepto, movimiento.Monto, movimiento.UsuarioId,
                movimiento.Fecha, movimiento.DocumentoReferenciaTipo, movimiento.DocumentoReferenciaId
            }, transaction, commandType: CommandType.StoredProcedure);
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
            "market.usp_MovimientoCaja_Listar", new { cajaId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<(decimal Ingresos, decimal Egresos)> ObtenerTotalesMovimientosAsync(int cajaId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleAsync<(decimal Ingresos, decimal Egresos)>(
                "market.usp_MovimientoCaja_ObtenerTotales", new { cajaId }, transaction, commandType: CommandType.StoredProcedure);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }
}
