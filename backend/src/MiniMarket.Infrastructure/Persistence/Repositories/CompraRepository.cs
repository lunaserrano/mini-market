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
        return await transaction.Connection!.QuerySingleAsync<int>("market.usp_Compra_Crear", new
        {
            compra.EmpresaId, compra.SucursalId, compra.ProveedorId, compra.UsuarioId, compra.NumeroDocumentoProveedor,
            compra.Fecha, compra.Subtotal, compra.ImpuestoTotal, compra.Total, compra.Estado
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task CrearDetalleAsync(DetalleCompra detalle, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync("market.usp_DetalleCompra_Crear", new
        {
            detalle.CompraId, detalle.ProductoId, detalle.TipoPrecioId, detalle.Cantidad,
            detalle.CantidadBaseCalculada, detalle.CostoUnitario, detalle.Subtotal
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<Compra?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Compra>(
            "market.usp_Compra_ObtenerEntidad", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<CompraDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Compra_ObtenerDetalle", new { empresaId, id }, commandType: CommandType.StoredProcedure);

        var compra = await multi.ReadSingleOrDefaultAsync<CompraCabeceraTmp>();
        var detalles = await multi.ReadAsync<DetalleCompraDto>();
        if (compra is null) return null;

        return new CompraDto(compra.Id, compra.Fecha, compra.ProveedorId, compra.ProveedorNombre, compra.NumeroDocumentoProveedor,
            compra.Subtotal, compra.ImpuestoTotal, compra.Total, compra.Estado, detalles.AsList());
    }

    public async Task<IReadOnlyList<CompraResumenDto>> ListarAsync(int empresaId, int? sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<CompraResumenDto>(
            "market.usp_Compra_Listar", new { empresaId, sucursalId }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task AnularAsync(int id, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "market.usp_Compra_Anular", new { id }, transaction, commandType: CommandType.StoredProcedure);
    }

    private record CompraCabeceraTmp(int Id, DateTime Fecha, int ProveedorId, string ProveedorNombre,
        string? NumeroDocumentoProveedor, decimal Subtotal, decimal ImpuestoTotal, decimal Total, string Estado);
}
