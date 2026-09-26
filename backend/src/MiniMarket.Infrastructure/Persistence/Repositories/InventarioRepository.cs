using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class InventarioRepository : IInventarioRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InventarioRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<InventarioDto>> ListarAsync(int empresaId, int? sucursalId, int? productoId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<InventarioDto>(
            "market.usp_Inventario_Listar", new { empresaId, sucursalId, productoId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<Inventario?> ObtenerAsync(int productoId, int sucursalId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<Inventario>(
                "market.usp_Inventario_Obtener", new { productoId, sucursalId }, transaction, commandType: CommandType.StoredProcedure);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task<Inventario?> ObtenerParaActualizarAsync(int productoId, int sucursalId, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Inventario>(
            "market.usp_Inventario_ObtenerParaActualizar", new { productoId, sucursalId }, transaction,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(Inventario inventario, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleAsync<int>("market.usp_Inventario_Crear", new
            {
                inventario.ProductoId, inventario.SucursalId, inventario.StockActual,
                inventario.StockMinimo, inventario.FechaActualizacion
            }, transaction, commandType: CommandType.StoredProcedure);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarStockAsync(int productoId, int sucursalId, decimal nuevoStock, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "market.usp_Inventario_ActualizarStock", new { productoId, sucursalId, nuevoStock }, transaction,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> RegistrarMovimientoAsync(MovimientoInventario movimiento, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleAsync<int>("market.usp_MovimientoInventario_Registrar", new
        {
            movimiento.EmpresaId, movimiento.SucursalId, movimiento.ProductoId, movimiento.UsuarioId,
            movimiento.TipoMovimiento, movimiento.Cantidad, movimiento.StockResultante,
            movimiento.DocumentoOrigenTipo, movimiento.DocumentoOrigenId, movimiento.Observacion, movimiento.FechaMovimiento
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> ListarMovimientosAsync(int empresaId, MovimientoInventarioFiltro filtro)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<MovimientoInventarioDto>("market.usp_MovimientoInventario_Listar", new
        {
            empresaId,
            filtro.ProductoId,
            filtro.SucursalId,
            filtro.Desde,
            filtro.Hasta
        }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }
}
