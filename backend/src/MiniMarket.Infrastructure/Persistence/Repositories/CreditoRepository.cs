using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class CreditoRepository : ICreditoRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CreditoRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CrearAsync(Credito credito, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleAsync<int>("market.usp_Credito_Crear", new
        {
            credito.EmpresaId, credito.VentaId, credito.ClienteId, credito.MontoOriginal, credito.SaldoPendiente,
            credito.Estado, credito.FechaVencimiento, credito.FechaCreacion, credito.CreadoPorUsuarioId
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<Credito?> ObtenerParaActualizarAsync(int empresaId, int id, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Credito>(
            "market.usp_Credito_ObtenerParaActualizar", new { empresaId, id }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<Credito?> ObtenerPorVentaAsync(int ventaId, IDbTransaction transaction)
    {
        return await transaction.Connection!.QuerySingleOrDefaultAsync<Credito>(
            "market.usp_Credito_ObtenerPorVenta", new { ventaId }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ContarAbonosAsync(int creditoId, IDbTransaction transaction) =>
        await transaction.Connection!.QuerySingleAsync<int>(
            "market.usp_AbonoCredito_Contar", new { creditoId }, transaction, commandType: CommandType.StoredProcedure);

    public async Task RegistrarAbonoAsync(AbonoCredito abono, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync("market.usp_AbonoCredito_Registrar", new
        {
            abono.CreditoId, abono.CajaId, abono.UsuarioId, abono.Metodo, abono.Monto, abono.Referencia, abono.Fecha
        }, transaction, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarSaldoAsync(int id, decimal saldoPendiente, string estado, DateTime? fechaCancelacion, IDbTransaction transaction)
    {
        await transaction.Connection!.ExecuteAsync(
            "market.usp_Credito_ActualizarSaldo", new { id, saldoPendiente, estado, fechaCancelacion }, transaction,
            commandType: CommandType.StoredProcedure);
    }

    public async Task AnularAsync(int id, IDbTransaction transaction) =>
        await transaction.Connection!.ExecuteAsync(
            "market.usp_Credito_Anular", new { id }, transaction, commandType: CommandType.StoredProcedure);

    public async Task<IReadOnlyList<CreditoResumenDto>> ListarAsync(int empresaId, int? clienteId, string? estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var items = await connection.QueryAsync<CreditoResumenDto>(
            "market.usp_Credito_Listar", new { empresaId, clienteId, estado }, commandType: CommandType.StoredProcedure);
        return items.AsList();
    }

    public async Task<CreditoDto?> ObtenerDetalleAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var multi = await connection.QueryMultipleAsync(
            "market.usp_Credito_ObtenerDetalle", new { empresaId, id }, commandType: CommandType.StoredProcedure);

        var cabecera = await multi.ReadSingleOrDefaultAsync<CabeceraCredito>();
        var abonos = (await multi.ReadAsync<AbonoCreditoDto>()).AsList();
        if (cabecera is null) return null;

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
