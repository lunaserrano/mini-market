using System.Data;
using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Implementación concreta de <see cref="IUnitOfWork"/>: abre una conexión y arranca una transacción
/// inmediatamente. Si se hace Dispose sin haber llamado Commit(), la transacción se revierte
/// (comportamiento estándar de SqlTransaction.Dispose sin Commit previo) — reemplaza la garantía
/// que EF Core daría con SaveChanges.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private readonly IDbTransaction _transaction;
    private bool _committed;
    private bool _disposed;

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connection = connectionFactory.CreateOpenConnection();
        _transaction = _connection.BeginTransaction();
    }

    public IDbConnection Connection => _connection;
    public IDbTransaction Transaction => _transaction;

    public void Commit()
    {
        _transaction.Commit();
        _committed = true;
    }

    public void Rollback()
    {
        _transaction.Rollback();
        _committed = true; // evita un segundo Rollback en Dispose
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_committed)
        {
            try { _transaction.Rollback(); } catch (InvalidOperationException) { /* la conexión ya pudo cerrarse */ }
        }
        _transaction.Dispose();
        _connection.Dispose();
    }
}

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IUnitOfWork Create() => new UnitOfWork(_connectionFactory);
}
