using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class CreditoRepository : ICreditoRepository
{
    private const string VencidoSql =
        "CAST(CASE WHEN cr.Estado = 'PENDIENTE' AND cr.FechaVencimiento IS NOT NULL AND cr.FechaVencimiento < SYSUTCDATETIME() THEN 1 ELSE 0 END AS bit)";

    private readonly IDbConnectionFactory _connectionFactory;

    public CreditoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CrearAsync(Credito credito, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO Credito (EmpresaId, VentaId, ClienteId, MontoOriginal, SaldoPendiente, Estado,
                FechaVencimiento, FechaCreacion, CreadoPorUsuarioId)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @VentaId, @ClienteId, @MontoOriginal, @SaldoPendiente, @Estado,
                @FechaVencimiento, @FechaCreacion, @CreadoPorUsuarioId)
            """;
        return await transaction.Connection!.QuerySingleAsync<int>(sql, credito, transaction);
    }

    public async Task<Credito?> ObtenerParaActualizarAsync(int empresaId, int id, IDbTransaction transaction)
    {
        const string sql = "SELECT * FROM Credito WITH (UPDLOCK, ROWLOCK) WHERE EmpresaId = @empresaId AND Id = @id";
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Credito>(sql, new { empresaId, id }, transaction);
    }

    public async Task<Credito?> ObtenerPorVentaAsync(int ventaId, IDbTransaction transaction)
    {
        const string sql = "SELECT * FROM Credito WITH (UPDLOCK, ROWLOCK) WHERE VentaId = @ventaId";
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Credito>(sql, new { ventaId }, transaction);
    }

    public async Task<int> ContarAbonosAsync(int creditoId, IDbTransaction transaction) =>
        await transaction.Connection!.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM AbonoCredito WHERE CreditoId = @creditoId", new { creditoId }, transaction);

    public async Task RegistrarAbonoAsync(AbonoCredito abono, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO AbonoCredito (CreditoId, CajaId, UsuarioId, Metodo, Monto, Referencia, Fecha)
            VALUES (@CreditoId, @CajaId, @UsuarioId, @Metodo, @Monto, @Referencia, @Fecha)
            """;
        await transaction.Connection!.ExecuteAsync(sql, abono, transaction);
    }

    public async Task ActualizarSaldoAsync(int id, decimal saldoPendiente, string estado, DateTime? fechaCancelacion, IDbTransaction transaction)
    {
        const string sql = """
            UPDATE Credito SET SaldoPendiente = @saldoPendiente, Estado = @estado, FechaCancelacion = @fechaCancelacion
            WHERE Id = @id
            """;
        await transaction.Connection!.ExecuteAsync(sql, new { id, saldoPendiente, estado, fechaCancelacion }, transaction);
    }

    public async Task AnularAsync(int id, IDbTransaction transaction) =>
        await transaction.Connection!.ExecuteAsync("UPDATE Credito SET Estado = 'ANULADO' WHERE Id = @id", new { id }, transaction);

    public async Task<IReadOnlyList<CreditoResumenDto>> ListarAsync(int empresaId, int? clienteId, string? estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = $"""
            SELECT cr.Id, cr.VentaId, v.Folio AS VentaFolio, cr.ClienteId, c.Nombre AS ClienteNombre,
                   cr.FechaCreacion, cr.FechaVencimiento, cr.MontoOriginal, cr.SaldoPendiente, cr.Estado,
                   {VencidoSql} AS Vencido
            FROM Credito cr
            INNER JOIN Venta v ON v.Id = cr.VentaId
            INNER JOIN Cliente c ON c.Id = cr.ClienteId
            WHERE cr.EmpresaId = @empresaId
              AND (@clienteId IS NULL OR cr.ClienteId = @clienteId)
              AND (@estado IS NULL OR cr.Estado = @estado)
            ORDER BY CASE WHEN cr.Estado = 'PENDIENTE' THEN 0 ELSE 1 END, cr.FechaCreacion DESC
            """;
        var items = await connection.QueryAsync<CreditoResumenDto>(sql, new { empresaId, clienteId, estado });
        return items.AsList();
    }

    public async Task<CreditoDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        const string sql = $"""
            SELECT cr.Id, cr.VentaId, v.Folio AS VentaFolio, cr.ClienteId, c.Nombre AS ClienteNombre,
                   cr.FechaCreacion, cr.FechaVencimiento, cr.FechaCancelacion,
                   v.Total AS TotalVenta, cr.MontoOriginal, cr.SaldoPendiente, cr.Estado,
                   {VencidoSql} AS Vencido
            FROM Credito cr
            INNER JOIN Venta v ON v.Id = cr.VentaId
            INNER JOIN Cliente c ON c.Id = cr.ClienteId
            WHERE cr.EmpresaId = @empresaId AND cr.Id = @id
            """;
        var cabecera = await connection.QuerySingleOrDefaultAsync<CabeceraCredito>(sql, new { empresaId, id });
        if (cabecera is null) return null;

        const string abonosSql = """
            SELECT a.Id, a.Fecha, a.Metodo, a.Monto, a.Referencia, u.NombreCompleto AS UsuarioNombre
            FROM AbonoCredito a
            INNER JOIN Usuario u ON u.Id = a.UsuarioId
            WHERE a.CreditoId = @id
            ORDER BY a.Fecha DESC, a.Id DESC
            """;
        var abonos = (await connection.QueryAsync<AbonoCreditoDto>(abonosSql, new { id })).AsList();

        return new CreditoDto(cabecera.Id, cabecera.VentaId, cabecera.VentaFolio, cabecera.ClienteId, cabecera.ClienteNombre,
            cabecera.FechaCreacion, cabecera.FechaVencimiento, cabecera.FechaCancelacion,
            cabecera.TotalVenta, cabecera.MontoOriginal, cabecera.SaldoPendiente, cabecera.Estado, cabecera.Vencido, abonos);
    }

    /// <summary>Fila plana del detalle; se completa con los abonos para armar <see cref="CreditoDto"/>.</summary>
    private sealed record CabeceraCredito(
        int Id, int VentaId, int VentaFolio, int ClienteId, string ClienteNombre,
        DateTime FechaCreacion, DateTime? FechaVencimiento, DateTime? FechaCancelacion,
        decimal TotalVenta, decimal MontoOriginal, decimal SaldoPendiente, string Estado, bool Vencido);
}
