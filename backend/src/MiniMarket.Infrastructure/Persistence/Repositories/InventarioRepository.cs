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
        const string sql = """
            SELECT i.ProductoId, p.Nombre AS ProductoNombre, i.SucursalId, i.StockActual, i.StockMinimo
            FROM Inventario i
            INNER JOIN Producto p ON p.Id = i.ProductoId
            WHERE p.EmpresaId = @empresaId
              AND (@sucursalId IS NULL OR i.SucursalId = @sucursalId)
              AND (@productoId IS NULL OR i.ProductoId = @productoId)
            ORDER BY p.Nombre
            """;
        var items = await connection.QueryAsync<InventarioDto>(sql, new { empresaId, sucursalId, productoId });
        return items.AsList();
    }

    public async Task<Inventario?> ObtenerAsync(int productoId, int sucursalId, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<Inventario>(
                "SELECT * FROM Inventario WHERE ProductoId = @productoId AND SucursalId = @sucursalId",
                new { productoId, sucursalId }, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task<Inventario?> ObtenerParaActualizarAsync(int productoId, int sucursalId, IDbTransaction transaction)
    {
        // UPDLOCK+ROWLOCK evita que dos ventas concurrentes lean el mismo stock antes de que la primera
        // haga commit (protección contra condiciones de carrera al descontar inventario).
        const string sql = """
            SELECT * FROM Inventario WITH (UPDLOCK, ROWLOCK)
            WHERE ProductoId = @productoId AND SucursalId = @sucursalId
            """;
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Inventario>(sql, new { productoId, sucursalId }, transaction);
    }

    public async Task<int> CrearAsync(Inventario inventario, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? _connectionFactory.CreateOpenConnection();
        try
        {
            const string sql = """
                INSERT INTO Inventario (ProductoId, SucursalId, StockActual, StockMinimo, FechaActualizacion)
                OUTPUT INSERTED.Id
                VALUES (@ProductoId, @SucursalId, @StockActual, @StockMinimo, @FechaActualizacion)
                """;
            return await connection.QuerySingleAsync<int>(sql, inventario, transaction);
        }
        finally
        {
            if (transaction is null) connection.Dispose();
        }
    }

    public async Task ActualizarStockAsync(int productoId, int sucursalId, decimal nuevoStock, IDbTransaction transaction)
    {
        const string sql = """
            UPDATE Inventario SET StockActual = @nuevoStock, FechaActualizacion = SYSUTCDATETIME()
            WHERE ProductoId = @productoId AND SucursalId = @sucursalId
            """;
        await transaction.Connection!.ExecuteAsync(sql, new { productoId, sucursalId, nuevoStock }, transaction);
    }

    public async Task<int> RegistrarMovimientoAsync(MovimientoInventario movimiento, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO MovimientoInventario (EmpresaId, SucursalId, ProductoId, UsuarioId, TipoMovimiento, Cantidad,
                StockResultante, DocumentoOrigenTipo, DocumentoOrigenId, Observacion, FechaMovimiento)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @ProductoId, @UsuarioId, @TipoMovimiento, @Cantidad,
                @StockResultante, @DocumentoOrigenTipo, @DocumentoOrigenId, @Observacion, @FechaMovimiento)
            """;
        return await transaction.Connection!.QuerySingleAsync<int>(sql, movimiento, transaction);
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> ListarMovimientosAsync(int empresaId, MovimientoInventarioFiltro filtro)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            SELECT m.Id, m.ProductoId, p.Nombre AS ProductoNombre, m.SucursalId, m.TipoMovimiento, m.Cantidad,
                   m.StockResultante, m.DocumentoOrigenTipo, m.DocumentoOrigenId, m.Observacion, m.FechaMovimiento
            FROM MovimientoInventario m
            INNER JOIN Producto p ON p.Id = m.ProductoId
            WHERE m.EmpresaId = @empresaId
              AND (@productoId IS NULL OR m.ProductoId = @productoId)
              AND (@sucursalId IS NULL OR m.SucursalId = @sucursalId)
              AND (@desde IS NULL OR m.FechaMovimiento >= @desde)
              AND (@hasta IS NULL OR m.FechaMovimiento <= @hasta)
            ORDER BY m.FechaMovimiento DESC
            """;
        var items = await connection.QueryAsync<MovimientoInventarioDto>(sql, new
        {
            empresaId,
            filtro.ProductoId,
            filtro.SucursalId,
            filtro.Desde,
            filtro.Hasta
        });
        return items.AsList();
    }
}
