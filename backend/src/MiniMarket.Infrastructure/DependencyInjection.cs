using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Infrastructure.Persistence;
using MiniMarket.Infrastructure.Persistence.Repositories;
using MiniMarket.Infrastructure.Services;

namespace MiniMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la connection string 'DefaultConnection'.");

        // AddHttpContextAccessor() se registra en MiniMarket.Api (Program.cs): el paquete que provee
        // esa extensión (Microsoft.AspNetCore.Http) solo está disponible ahí, no en este classlib.

        services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
        services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped<ITenantContext, TenantContext>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // Repositorios (Dapper) — Scoped es suficiente porque no mantienen estado entre llamadas,
        // salvo abrir/cerrar su propia conexión por operación.
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRolRepository, RolRepository>();
        services.AddScoped<IEmpresaRepository, EmpresaRepository>();
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IInventarioRepository, InventarioRepository>();
        services.AddScoped<ICajaRepository, CajaRepository>();
        services.AddScoped<IVentaRepository, VentaRepository>();
        services.AddScoped<ICompraRepository, CompraRepository>();

        // Services de Application (orquestan repos + reglas de negocio).
        services.AddScoped<AuthService>();
        services.AddScoped<EmpresaService>();
        services.AddScoped<CategoriaService>();
        services.AddScoped<ProveedorService>();
        services.AddScoped<ClienteService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<InventarioService>();
        services.AddScoped<CajaService>();
        services.AddScoped<VentaService>();
        services.AddScoped<CompraService>();
        services.AddScoped<UsuarioService>();

        return services;
    }
}
