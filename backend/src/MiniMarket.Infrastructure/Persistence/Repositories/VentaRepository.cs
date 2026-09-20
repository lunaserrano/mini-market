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
        // MAX+1 dentro de la misma transacción; combinado con el índice único (SucursalId, Folio) y el
        // UPDLOCK implícito de la transacción de venta, evita folios duplicados bajo concurrencia normal.
        const string sql = "SELECT ISNULL(MAX(Folio), 0) + 1 FROM Venta WITH (UPDLOCK, HOLDLOCK) WHERE SucursalId = @sucursalId";
        return await transaction.Connection!.QuerySingleAsync<int>(sql, new { sucursalId }, transaction);
    }

    public async Task<int> CrearAsync(Venta venta, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO Venta (EmpresaId, SucursalId, CajaId, ClienteId, UsuarioId, Folio, Fecha,
                Subtotal, DescuentoTotal, ImpuestoTotal, Total, Estado)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @CajaId, @ClienteId, @UsuarioId, @Folio, @Fecha,
                @Subtotal, @DescuentoTotal, @ImpuestoTotal, @Total, @Estado)
            """;
        return await transaction.Connection!.QuerySingleAsync<int>(sql, venta, transaction);
    }

    public async Task CrearDetalleAsync(DetalleVenta detalle, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO DetalleVenta (VentaId, ProductoId, TipoPrecioId, Cantidad, CantidadBaseCalculada, PrecioUnitario, Descuento, Subtotal)
            VALUES (@VentaId, @ProductoId, @TipoPrecioId, @Cantidad, @CantidadBaseCalculada, @PrecioUnitario, @Descuento, @Subtotal)
            """;
        await transaction.Connection!.ExecuteAsync(sql, detalle, transaction);
    }

    public async Task CrearPagoAsync(PagoVenta pago, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO PagoVenta (VentaId, Metodo, Monto, Referencia, Fecha)
            VALUES (@VentaId, @Metodo, @Monto, @Referencia, @Fecha)
            """;
        await transaction.Connection!.ExecuteAsync(sql, pago, transaction);
    }

    public async Task<Venta?> ObtenerEntidadAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Venta>(
            "SELECT * FROM Venta WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
    }

    public async Task<IReadOnlyList<DetalleVenta>> ObtenerDetallesEntidadAsync(int ventaId, IDbTransaction transaction)
    {
        const string sql = "SELECT * FROM DetalleVenta WHERE VentaId = @ventaId";
        var detalles = await transaction.Connection!.QueryAsync<DetalleVenta>(sql, new { ventaId }, transaction);
        return detalles.AsList();
    }

    public async Task<VentaDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var venta = await connection.QuerySingleOrDefaultAsync<Venta>(
            "SELECT * FROM Venta WHERE EmpresaId = @empresaId AND Id = @id", new { empresaId, id });
        if (venta is null) return null;

        const string detalleSql = """
            SELECT d.ProductoId, p.Nombre AS ProductoNombre, d.TipoPrecioId, tp.Nombre AS TipoPrecioNombre,
                   d.Cantidad, d.CantidadBaseCalculada, d.PrecioUnitario, d.Descuento, d.Subtotal
            FROM DetalleVenta d
            INNER JOIN Producto p ON p.Id = d.ProductoId
            INNER JOIN TipoPrecio tp ON tp.Id = d.TipoPrecioId
            WHERE d.VentaId = @id
            """;
        var detalles = await connection.QueryAsync<DetalleVentaDto>(detalleSql, new { id });

        var pagos = await connection.QueryAsync<PagoVentaDto>(
            "SELECT Metodo, Monto, Referencia FROM PagoVenta WHERE VentaId = @id", new { id });

        return new VentaDto(venta.Id, venta.Folio, venta.Fecha, venta.ClienteId, venta.Estado,
            venta.Subtotal, venta.DescuentoTotal, venta.ImpuestoTotal, venta.Total,
            detalles.AsList(), pagos.AsList());
    }

    public async Task<IReadOnlyList<VentaResumenDto>> ListarAsync(int empresaId, int? sucursalId, int? usuarioId, int? cajaId, DateTime? desde, DateTime? hasta)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            SELECT v.Id, v.Folio, v.Fecha, c.Nombre AS ClienteNombre, v.Total, v.Estado,
                   CASE WHEN cr.Estado = 'PENDIENTE' THEN cr.SaldoPendiente END AS SaldoCredito
            FROM Venta v
            LEFT JOIN Cliente c ON c.Id = v.ClienteId
            LEFT JOIN Credito cr ON cr.VentaId = v.Id
            WHERE v.EmpresaId = @empresaId
              AND (@sucursalId IS NULL OR v.SucursalId = @sucursalId)
              AND (@usuarioId IS NULL OR v.UsuarioId = @usuarioId)
              AND (@cajaId IS NULL OR v.CajaId = @cajaId)
              AND (@desde IS NULL OR v.Fecha >= @desde)
              AND (@hasta IS NULL OR v.Fecha <= @hasta)
            ORDER BY v.Fecha DESC
            """;
        var items = await connection.QueryAsync<VentaResumenDto>(sql, new { empresaId, sucursalId, usuarioId, cajaId, desde, hasta });
        return items.AsList();
    }

    public async Task AnularAsync(int id, int usuarioAnulacionId, string motivo, IDbTransaction transaction)
    {
        const string sql = """
            UPDATE Venta SET Estado = 'ANULADA', UsuarioAnulacionId = @usuarioAnulacionId,
                MotivoAnulacion = @motivo, FechaAnulacion = SYSUTCDATETIME()
            WHERE Id = @id
            """;
        await transaction.Connection!.ExecuteAsync(sql, new { id, usuarioAnulacionId, motivo }, transaction);
    }
}
