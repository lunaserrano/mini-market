using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class VentaRepository : IVentaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public VentaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> ObtenerSiguienteFolioAsync(int sucursalId, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleAsync<int>(
            "market.usp_Venta_ObtenerSiguienteFolio", new { sucursalId }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(Venta venta, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleAsync<int>("market.usp_Venta_Crear", new
        {
            venta.EmpresaId, venta.SucursalId, venta.CajaId, venta.ClienteId, venta.UsuarioId, venta.Folio, venta.Fecha,
            venta.Subtotal, venta.DescuentoTotal, venta.ImpuestoTotal, venta.Total, venta.Estado
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task CrearDetalleAsync(DetalleVenta detalle, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync("market.usp_DetalleVenta_Crear", new
        {
            detalle.VentaId, detalle.ProductoId, detalle.TipoPrecioId, detalle.Cantidad,
            detalle.CantidadBaseCalculada, detalle.PrecioUnitario, detalle.Descuento, detalle.Subtotal
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task CrearPagoAsync(PagoVenta pago, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync("market.usp_PagoVenta_Crear", new
        {
            pago.VentaId, pago.Metodo, pago.Monto, pago.Referencia, pago.Fecha
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<Venta?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Venta>(
            "market.usp_Venta_ObtenerEntidad", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<DetalleVenta>> ObtenerDetallesEntidadAsync(int ventaId, IDbTransaction transaction)
    {
        var detalles = await transaction.Connection!.QueryAsync<DetalleVenta>(
            "market.usp_DetalleVenta_ListarEntidad", new { ventaId }, transaction, commandType: CommandType.StoredProcedure);
        return detalles.AsList();
    }

    public async Task<VentaDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Venta_ObtenerDetalle", new { empresaId, id }, commandType: CommandType.StoredProcedure);

        var venta = await multi.ReadSingleOrDefaultAsync<Venta>();
        var detalles = await multi.ReadAsync<DetalleVentaDto>();
        var pagos = await multi.ReadAsync<PagoVentaDto>();
        if (venta is null) return null;

        return new VentaDto(venta.Id, venta.Folio, venta.Fecha, venta.ClienteId, venta.Estado,
            venta.Subtotal, venta.DescuentoTotal, venta.ImpuestoTotal, venta.Total,
            detalles.AsList(), pagos.AsList());
    }

    public async Task<IReadOnlyList<VentaResumenDto>> ListarAsync(int empresaId, int? sucursalId, int? usuarioId, int? cajaId, DateTime? desde, DateTime? hasta)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<VentaResumenDto>("market.usp_Venta_Listar",
            new { empresaId, sucursalId, usuarioId, cajaId, desde, hasta }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task AnularAsync(int id, int usuarioAnulacionId, string motivo, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "market.usp_Venta_Anular", new { id, usuarioAnulacionId, motivo }, transaction, commandType: CommandType.StoredProcedure);
    }
}
