using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StaffValidator.WebApp.Data;
using StaffValidator.WebApp.Repositories;

// Serilog setup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("Logs/staff-validator-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("🚀 Starting StaffValidator Web Application...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllersWithViews();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Staff Validator API",
            Version = "v1",
            Description = "Professional staff validation and management API with comprehensive CRUD operations",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Staff Validator Team",
                Email = "support@staffvalidator.com"
            }
        });

        // XML docs
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Swagger: JWT security
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
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
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Repository from config
    builder.Services.Configure<StaffRepositoryOptions>(builder.Configuration.GetSection("Data"));
    builder.Services.AddSingleton<IStaffRepository>(sp =>
        new StaffRepository(sp.GetRequiredService<IOptions<StaffRepositoryOptions>>()));

    // DI: validators/auth & HybridValidation options

    // Data layer: choose DB (SQLite) or fallback to JSON file based on config
    var dataSection = builder.Configuration.GetSection("Data");
    var useDb = dataSection.GetValue<bool>("UseDatabase");
    var provider = dataSection.GetValue<string>("Provider") ?? "sqlite";
    var connStr = dataSection.GetValue<string>("ConnectionString") ?? "Data Source=staff.db";

    if (useDb)
    {
        if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContext<StaffDbContext>(options => options.UseSqlite(connStr));
        }
        else if (provider.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContext<StaffDbContext>(options => options.UseSqlServer(connStr));
        }
        else
        {
            throw new InvalidOperationException($"Unsupported Data.Provider '{provider}'. Use 'sqlite' or 'sqlserver'.");
        }

        builder.Services.AddScoped<IStaffRepository, EfStaffRepository>();
    }
    else
    {
        // JSON repository fallback
        builder.Services.Configure<StaffRepositoryOptions>(dataSection);
        builder.Services.AddSingleton<IStaffRepository>(sp =>
            new StaffRepository(sp.GetRequiredService<IOptions<StaffRepositoryOptions>>()));
    }

    // Register application services
    // Configure HybridValidation options from configuration
    builder.Services.Configure<HybridValidationOptions>(builder.Configuration.GetSection("HybridValidation"));

    // Register HybridValidatorService as singleton (for both base and concrete type)
    builder.Services.AddSingleton<HybridValidatorService>();
    builder.Services.AddSingleton<ValidatorService>(sp => sp.GetRequiredService<HybridValidatorService>());
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

    // Rate limiter: partition by client IP, 5 requests/min for auth policy
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1)
                }));
    });

    // Configure JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "StaffValidator-Super-Secret-Key-For-JWT-Tokens-2024!";
    var issuer = jwtSettings["Issuer"] ?? "StaffValidator";
    var audience = jwtSettings["Audience"] ?? "StaffValidator-Users";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Read JWT from cookie
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["AuthToken"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (!context.Handled && context.Request.Path.HasValue && !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                {
                    context.HandleResponse();
                    var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                    context.Response.Redirect($"/Auth/Login?returnUrl={returnUrl}");
                }
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
        options.AddPolicy("Manager", policy => policy.RequireRole("Administrator", "Manager"));
        options.AddPolicy("User", policy => policy.RequireRole("Administrator", "Manager", "User"));
    });

    var app = builder.Build();

    Log.Information("✅ Application configured successfully. Starting web server...");

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
    }
    else
    {
        // Swagger in development
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Staff Validator API v1");
            options.RoutePrefix = "api/docs";
            options.DocumentTitle = "Staff Validator API Documentation";
            options.DefaultModelsExpandDepth(-1);
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        });
    }

    app.UseStaticFiles();
    app.UseRouting();

    // Add authentication and authorization middleware
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapGet("/api/auth/login", (HttpContext ctx) =>
    {
        ctx.Response.Redirect("/Auth/Login");
        return Results.Empty;
    }).AllowAnonymous();

    // Ensure database exists and import from JSON on first run if empty
    if (useDb)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaffDbContext>();
        // For now, EnsureCreated to keep zero-migration setup working for both SQLite/SQL Server
        db.Database.EnsureCreated();

        try
        {
            if (!db.Staff.Any())
            {
                var jsonPath = dataSection.GetValue<string>("JsonPath") ?? "data/staff_records.json";
                if (System.IO.File.Exists(jsonPath))
                {
                    var json = System.IO.File.ReadAllText(jsonPath);
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<StaffValidator.Core.Models.Staff>>(json) ?? new();
                    if (items.Count > 0)
                    {
                        db.Staff.AddRange(items);
                        db.SaveChanges();
                        Log.Information("Imported {Count} staff records from JSON into database", items.Count);
                    }
                }
            }
        }
        catch (Exception importEx)
        {
            Log.Warning(importEx, "Failed to import seed data into database");
        }
    }

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Staff}/{action=Index}/{id?}");

    Log.Information("✅ Application configured successfully. Starting web server...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
