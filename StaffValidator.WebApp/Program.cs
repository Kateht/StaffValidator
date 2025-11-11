using Microsoft.Extensions.Options;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Configure Serilog early in the application lifecycle
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
    
    // Add Serilog to the host
    builder.Host.UseSerilog();
    
    // Add controllers and views
    builder.Services.AddControllersWithViews();
    
    // Add API Explorer and Swagger
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

        // Include XML comments
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // Add JWT authentication to Swagger
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
    
    // Configure JSON Repository with sample data
    builder.Services.Configure<StaffRepositoryOptions>(builder.Configuration.GetSection("Data"));
    builder.Services.AddSingleton<IStaffRepository>(sp =>
        new StaffRepository(sp.GetRequiredService<IOptions<StaffRepositoryOptions>>()));
    
    // Register application services
    // Configure HybridValidation options from configuration
    builder.Services.Configure<HybridValidationOptions>(builder.Configuration.GetSection("HybridValidation"));

    // Use the HybridValidatorService as the concrete implementation for ValidatorService
    builder.Services.AddSingleton<ValidatorService, HybridValidatorService>();
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

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

        // Handle tokens from cookies for web app
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
        // Enable Swagger in development
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

// Expose Program class for integration testing (WebApplicationFactory)
public partial class Program { }
