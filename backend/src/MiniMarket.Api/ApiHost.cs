using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MiniMarket.Api.Authorization;
using MiniMarket.Api.Configuration;
using MiniMarket.Api.Controllers;
using MiniMarket.Api.Filters;
using MiniMarket.Api.Middleware;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Validators;
using MiniMarket.Infrastructure;
using MiniMarket.Infrastructure.Persistence;
using MiniMarket.Infrastructure.Services;

namespace MiniMarket.Api;

/// <summary>
/// Arranque de la Api, compartido por los dos modos de ejecución:
/// - Program.cs: proceso propio (Azure / desarrollo).
/// - MiniMarket.Desktop: la Api corre DENTRO del ejecutable WinForms (Kestrel en loopback, puerto
///   dinámico) y vive lo mismo que la ventana; no hay servicio Windows que instalar ni mantener.
/// </summary>
public static class ApiHost
{
    private const string CorsPolicyName = "ProductionOrDevCors";

    /// <summary>
    /// Crea el builder con todos los servicios registrados. <paramref name="archivosSecretos"/> son JSON
    /// opcionales con valores "ENC:" (DPAPI) que se agregan después de appsettings y se descifran en memoria.
    /// </summary>
    public static WebApplicationBuilder CrearBuilder(WebApplicationOptions opciones, params string[] archivosSecretos)
    {
        // ApplicationName fijo: MVC descubre los controllers en el ensamblado con ese nombre, y cuando la
        // Api se hospeda en MiniMarket.Desktop el ensamblado de entrada es el del cliente.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = opciones.Args,
            ContentRootPath = opciones.ContentRootPath,
            EnvironmentName = opciones.EnvironmentName,
            WebRootPath = opciones.WebRootPath,
            ApplicationName = typeof(ApiHost).Assembly.GetName().Name
        });

        foreach (var archivo in archivosSecretos)
            builder.Configuration.AddJsonFile(archivo, optional: true, reloadOnChange: false);
        builder.Configuration.AddDecryptedValues();

        RegistrarServicios(builder);
        return builder;
    }

    private static void RegistrarServicios(WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());

        // Validadores de FluentValidation (Application/Validators) — los ejecuta ValidationFilter. Mensajes en español.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");
        builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MiniMarket API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Bearer {token}\"",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<AuditoriaWriterService>();
        builder.Services.AddHostedService<SyncBackgroundService>();

        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración.");
        if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
            throw new InvalidOperationException(
                "Falta Jwt:Key (mínimo 32 caracteres). En modo Desktop configure la conexión desde la aplicación o con 'MiniMarket.ConfigTool init'.");
        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:DefaultConnection. En modo Desktop configure la conexión desde la aplicación o con 'MiniMarket.ConfigTool init'.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = TenantClaimTypes.Rol
                };
            });

        // Autorización por permisos
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, JsonAuthorizationResultHandler>();

        // Limita login/refresh por IP (Soporta cabeceras reenviadas de Azure)
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthController.RateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync("{\"error\":\"Demasiadas solicitudes. Intente de nuevo en un minuto.\"}", cancellationToken);
            };
        });

        // --- Configuración de CORS Dinámica (Soporta desarrollo y producción en Azure) ---
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                             ?? new[] { "http://localhost:4200" };

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });
    }

    /// <summary>Migraciones, catálogo de permisos y seed. Idempotente: corre en cada arranque.</summary>
    public static async Task InicializarBaseDatosAsync(WebApplication app, CancellationToken ct = default)
    {
        var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la connection string 'DefaultConnection'.");
        DatabaseMigrator.ApplyMigrations(connectionString);

        await PermisoCatalogSync.SyncAsync(app.Services.GetRequiredService<IDbConnectionFactory>());

        // En Desktop el seed crea la empresa, sucursal, roles y el admin inicial en la primera ejecución (idempotente).
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:Enabled"))
        {
            using var scope = app.Services.CreateScope();
            await DataSeeder.SeedAsync(
                scope.ServiceProvider.GetRequiredService<IEmpresaRepository>(),
                scope.ServiceProvider.GetRequiredService<ISucursalRepository>(),
                scope.ServiceProvider.GetRequiredService<IRolRepository>(),
                scope.ServiceProvider.GetRequiredService<IUsuarioRepository>(),
                scope.ServiceProvider.GetRequiredService<ICategoriaRepository>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        }
    }

    public static void ConfigurarPipeline(WebApplication app)
    {
        // 1. IMPORTANTE: Forwarded Headers debe ir primero para que Azure pase las IPs reales y esquema HTTPS
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Api:SwaggerEnabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // En Desktop la Api escucha solo HTTP en loopback (sin certificado): no redirigir a HTTPS.
        if (!app.Environment.IsEnvironment("Desktop"))
            app.UseHttpsRedirection();
        app.UseCors(CorsPolicyName);

        // Auditoría y excepciones
        app.UseMiddleware<AuditoriaMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();

        // Health check anónimo: el cliente WinForms lo consulta para saber si la Api y la BD responden.
        app.MapGet("/api/health", (IDbConnectionFactory db, IHostEnvironment env) =>
        {
            var dbOk = false;
            try
            {
                using var conexion = db.CreateOpenConnection();
                using var comando = conexion.CreateCommand();
                comando.CommandText = "SELECT 1";
                dbOk = Equals(comando.ExecuteScalar(), 1);
            }
            catch
            {
                // BD caída: se informa en el cuerpo, la Api sigue respondiendo.
            }

            var cuerpo = new
            {
                status = dbOk ? "ok" : "degradado",
                db = dbOk,
                version = typeof(ApiHost).Assembly.GetName().Version?.ToString(),
                modo = env.EnvironmentName,
                fechaUtc = DateTime.UtcNow
            };
            return dbOk ? Results.Ok(cuerpo) : Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();
    }
}
