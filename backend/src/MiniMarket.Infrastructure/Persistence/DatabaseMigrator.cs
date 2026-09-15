using System.Reflection;
using DbUp;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Aplica los scripts de database/migrations/*.sql (embebidos como recurso del ensamblado, ver
/// EmbeddedResource en MiniMarket.Infrastructure.csproj) en orden ascendente, registrando cada uno
/// aplicado en la tabla SchemaVersions. Reemplaza a "dotnet ef database update" al no usar EF Core.
/// </summary>
public static class DatabaseMigrator
{
    public static void ApplyMigrations(string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException($"Fallo al aplicar migraciones de base de datos: {result.Error}", result.Error);
    }
}
