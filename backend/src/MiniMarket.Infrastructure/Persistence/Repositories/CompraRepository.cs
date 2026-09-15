using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class CompraRepository : ICompraRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CompraRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CrearAsync(Compra compra, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO Compra (EmpresaId, SucursalId, ProveedorId, UsuarioId, NumeroDocumentoProveedor, Fecha,
                Subtotal, ImpuestoTotal, Total, Estado)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @ProveedorId, @UsuarioId, @NumeroDocumentoProveedor, @Fecha,
                @Subtotal, @ImpuestoTotal, @Total, @Estado)
            """;
        return await transaction.Connection!.QuerySingleAsync<int>(sql, compra, transaction);
    }

    public async Task CrearDetalleAsync(DetalleCompra detalle, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO DetalleCompra (CompraId, ProductoId, Cantidad, CantidadBaseCalculada, CostoUnitario, Subtotal)
            VALUES (@CompraId, @ProductoId, @Cantidad, @CantidadBaseCalculada, @CostoUnitario, @Subtotal)
            """;
        await transaction.Connection!.ExecuteAsync(sql, detalle, transaction);
    }

    public async Task<Compra?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Compra>(
            "SELECT * FROM Compra WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<CompraDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        const string compraSql = """
            SELECT c.Id, c.Fecha, c.ProveedorId, pr.Nombre AS ProveedorNombre, c.NumeroDocumentoProveedor,
                   c.Subtotal, c.ImpuestoTotal, c.Total, c.Estado
            FROM Compra c
            INNER JOIN Proveedor pr ON pr.Id = c.ProveedorId
            WHERE c.EmpresaId = @empresaId AND c.Id = @id
            """;
        var compra = await connection.QuerySingleOrDefaultAsync<CompraCabeceraTmp>(compraSql, new { empresaId, id });
        if (compra is null) return null;

        const string detalleSql = """
            SELECT d.ProductoId, p.Nombre AS ProductoNombre, d.Cantidad, d.CantidadBaseCalculada, d.CostoUnitario, d.Subtotal
            FROM DetalleCompra d
            INNER JOIN Producto p ON p.Id = d.ProductoId
            WHERE d.CompraId = @id
            """;
        var detalles = await connection.QueryAsync<DetalleCompraDto>(detalleSql, new { id });

        return new CompraDto(compra.Id, compra.Fecha, compra.ProveedorId, compra.ProveedorNombre, compra.NumeroDocumentoProveedor,
            compra.Subtotal, compra.ImpuestoTotal, compra.Total, compra.Estado, detalles.AsList());
    }

    public async Task<IReadOnlyList<CompraResumenDto>> ListarAsync(int empresaId, int? sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            SELECT c.Id, c.Fecha, pr.Nombre AS ProveedorNombre, c.Total, c.Estado
            FROM Compra c
            INNER JOIN Proveedor pr ON pr.Id = c.ProveedorId
            WHERE c.EmpresaId = @empresaId AND (@sucursalId IS NULL OR c.SucursalId = @sucursalId)
            ORDER BY c.Fecha DESC
            """;
        var items = await connection.QueryAsync<CompraResumenDto>(sql, new { empresaId, sucursalId });
        return items.AsList();
    }

    public async Task AnularAsync(int id, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "UPDATE Compra SET Estado = 'ANULADA' WHERE Id = @id", new { id }, transaction);
    }

    private record CompraCabeceraTmp(int Id, DateTime Fecha, int ProveedorId, string ProveedorNombre,
        string? NumeroDocumentoProveedor, decimal Subtotal, decimal ImpuestoTotal, decimal Total, string Estado);
}
