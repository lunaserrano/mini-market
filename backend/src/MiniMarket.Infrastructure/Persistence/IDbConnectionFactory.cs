using System.Data;
using Microsoft.Data.SqlClient;

namespace MiniMarket.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection CreateOpenConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
