using System.Data;

namespace MiniMarket.Application.Interfaces;

/// <summary>
/// Envuelve una conexión + transacción ADO.NET compartida entre varios repositorios Dapper dentro
/// de una misma operación de negocio (ej. crear una Venta: descuenta stock, inserta movimiento de
/// inventario, inserta venta+detalle+pagos — todo o nada). Sin EF Core no hay SaveChanges automático:
/// esta es la pieza que reemplaza esa garantía transaccional.
/// Uso típico en un Service:
///   using var uow = _unitOfWorkFactory.Create();
///   await _ventaRepository.CrearAsync(venta, uow);
///   ...
///   uow.Commit();
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction Transaction { get; }
    void Commit();
    void Rollback();
}

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
