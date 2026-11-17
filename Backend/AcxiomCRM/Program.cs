
using System.Security.Claims;
using System.Threading.RateLimiting;
using AcxiomCRM.Services;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. DATABASE CONFIGURATION
// ==========================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ==========================================
// 2. ASP.NET CORE IDENTITY CONFIGURATION
// ==========================================

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ==========================================
// 3. JWT CONFIGURATION
// ==========================================

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing. Configure Jwt:Key using User Secrets.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT Issuer is missing from configuration.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT Audience is missing from configuration.");

byte[] jwtKeyBytes;

try
{
    jwtKeyBytes = Convert.FromBase64String(jwtKey);
}
catch (FormatException)
{
    throw new InvalidOperationException(
        "JWT signing key must be a valid Base64 value.");
}

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

// ==========================================
// 4. AUTHENTICATION CONFIGURATION
// ==========================================

// Preserve Identity cookie authentication.
// Add JWT Bearer as an additional authentication scheme.

builder.Services
    .AddAuthentication()
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,

                    ValidateAudience = true,
                    ValidAudience = jwtAudience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(jwtKeyBytes),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
        });

// ==========================================
// 5. LOGIN API RATE LIMITING
// ==========================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",

            factory: partition =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,

                    Window = TimeSpan.FromMinutes(1),

                    QueueLimit = 0,

                    AutoReplenishment = true
                }));
});

// ==========================================
// 6. MVC CONFIGURATION
// ==========================================

builder.Services.AddControllersWithViews();

// ==========================================
// 7. SWAGGER CONFIGURATION
// ==========================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Rajib Connect API",
        Version = "v1",
        Description = "REST API documentation for Rajib Connect"
    });

    // JWT Bearer security definition

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Enter your JWT access token."
        });

    // Global JWT security requirement

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer", document)] = []
        });
});

// ==========================================
// 8. APPLICATION SERVICES
// ==========================================

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<AuditLogService>();

// ==========================================
// 9. BUILD APPLICATION
// ==========================================

var app = builder.Build();

// ==========================================
// 10. HTTP REQUEST PIPELINE
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/swagger/v1/swagger.json",
            "Rajib Connect API v1");

        options.RoutePrefix = "swagger";

        options.DocumentTitle =
            "Rajib Connect API Documentation";
    });
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Rate limiting middleware must follow routing.
app.UseRateLimiter();

// Authentication must precede authorization.
app.UseAuthentication();

app.UseAuthorization();

// ==========================================
// 11. ROUTE CONFIGURATION
// ==========================================

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapRazorPages();

// ==========================================
// 12. ROLE INITIALIZATION
// ==========================================

using (var scope = app.Services.CreateScope())
{
    await RoleInitializer.InitializeAsync(
        scope.ServiceProvider,
        app.Configuration);
}

// ==========================================
// 13. RUN APPLICATION
// ==========================================

app.Run();
