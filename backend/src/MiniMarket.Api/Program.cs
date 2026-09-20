using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using MiniMarket.Api.Authorization;
using MiniMarket.Api.Controllers;
using MiniMarket.Api.Filters;
using MiniMarket.Api.Middleware;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Validators;
using MiniMarket.Infrastructure;
using MiniMarket.Infrastructure.Persistence;
using MiniMarket.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración.");

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
            // El access token dura pocos minutos: la tolerancia por defecto (5 min) sería una fracción enorme de su vida.
            ClockSkew = TimeSpan.FromSeconds(30),
            // El claim "rol" (ver TenantClaimTypes) se emite con nombre corto, no como el URI estándar de ClaimTypes.Role.
            // Es informativo: el acceso se decide por permisos ([HasPermission]), no por rol.
            RoleClaimType = TenantClaimTypes.Rol
        };
    });

// Autorización por permisos: [HasPermission(Permisos.X)] → política "perm:x" → PermissionHandler (claims "permiso").
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, JsonAuthorizationResultHandler>();

// Limita login/refresh por IP (fuerza bruta distribuida entre cuentas; el bloqueo de cuenta cubre un mismo usuario).
// Detrás de un proxy inverso hay que configurar ForwardedHeaders para que RemoteIpAddress sea la IP real del cliente.
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

const string CorsPolicyDev = "DevCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyDev, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- Migraciones + seed (solo Development ejecuta el seed; las migraciones corren siempre) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la connection string 'DefaultConnection'.");
DatabaseMigrator.ApplyMigrations(connectionString);

// El catálogo de permisos se sincroniza en TODOS los entornos (antes del seed, que asigna permisos por defecto).
await PermisoCatalogSync.SyncAsync(app.Services.GetRequiredService<IDbConnectionFactory>());

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DataSeeder.SeedAsync(connectionFactory, passwordHasher);
}

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicyDev);
// Auditoría de actividad: va por fuera del manejo de excepciones para registrar el código de estado final.
app.UseMiddleware<AuditoriaMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
