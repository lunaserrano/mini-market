using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.WindowsServices;
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

// Como Windows Service (instalación desktop/offline) el directorio de trabajo es System32: el content root
// debe ser la carpeta del ejecutable para encontrar appsettings*.json.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});
builder.Host.UseWindowsService(options => options.ServiceName = "MiniMarketApi");

// Modo Desktop = API como servicio en la misma PC/LAN que el cliente WinForms, contra SQL Server Express
// (sin internet). Sus secretos viven en appsettings.Secrets.json (generado por MiniMarket.ConfigTool,
// fuera del repo) cifrados con DPAPI ("ENC:") y se descifran solo en memoria.
var esDesktop = builder.Environment.IsEnvironment("Desktop");
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);
builder.Configuration.AddDecryptedValues();

// --- Servicios ---
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
        "Falta Jwt:Key (mínimo 32 caracteres). En modo Desktop ejecute 'MiniMarket.ConfigTool init' para generarla.");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
    throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. En modo Desktop ejecute 'MiniMarket.ConfigTool init' para generarla.");

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
const string CorsPolicyName = "ProductionOrDevCors";
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- Migraciones + seed ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la connection string 'DefaultConnection'.");
DatabaseMigrator.ApplyMigrations(connectionString);

await PermisoCatalogSync.SyncAsync(app.Services.GetRequiredService<IDbConnectionFactory>());

// En Desktop el seed crea la empresa, sucursal, roles y el admin inicial en la primera ejecución (idempotente).
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Seed:Enabled"))
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

// --- Pipeline HTTP ---

// 1. IMPORTANTE: Forwarded Headers debe ir primero para que Azure pase las IPs reales y esquema HTTPS
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Api:SwaggerEnabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// En Desktop la Api escucha solo HTTP en loopback/LAN (sin certificado): no redirigir a HTTPS.
if (!esDesktop)
    app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);

// Auditoría y excepciones
app.UseMiddleware<AuditoriaMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

// Health check anónimo: el cliente WinForms lo consulta para saber si el servicio local y la BD responden.
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
        version = typeof(Program).Assembly.GetName().Version?.ToString(),
        modo = env.EnvironmentName,
        fechaUtc = DateTime.UtcNow
    };
    return dbOk ? Results.Ok(cuerpo) : Results.Json(cuerpo, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.Run();
