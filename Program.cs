using Amazon.Runtime;
using Amazon.S3;
using BE_ZSM.Contexts;
using BE_ZSM.Helpers;
using BE_ZSM.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;

public partial class Program
{
    private static void Main(string[] args)
    {
        LoadEnvironmentVariables();
        var builder = WebApplication.CreateBuilder(args);

        var awsAccessKeyId = GetRequiredEnvironmentVariable("ACCESS_KEY_ID");
        var awsSecretAccessKey = GetRequiredEnvironmentVariable("SECRET_ACCESS_KEY");
        var awsRegion = GetRequiredEnvironmentVariable("AWS_REGION");

        builder.Services.AddControllers();


        var connectionString =
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing."
            );

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString)
        );

        var jwtIssuer = GetRequiredEnvironmentVariable("JWT_ISSUER");
        var jwtAudience = GetRequiredEnvironmentVariable("JWT_AUDIENCE");
        var jwtKey = GetRequiredEnvironmentVariable("JWT_KEY");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters       
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,

                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name,
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAssertion(context =>
                {
                    var user = context.User;
                    string[] adminRoles = ["Admin", "ADMIN", "admin"];

                    return adminRoles.Any(role => user.IsInRole(role))
                        || user.Claims.Any(claim =>
                            (claim.Type == ClaimTypes.Role || claim.Type == "role")
                            && adminRoles.Any(role =>
                                string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase)));
                }));
        });

        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<RecordHelper>();
        builder.Services.AddScoped<DbSaveHelper>();
        builder.Services.AddScoped<RecordMapperHelper>();
        builder.Services.AddScoped<AdminAccessHelper>();
        builder.Services.AddScoped<S3PresignedUrlService>();

        builder.Services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(
                new BasicAWSCredentials(
                    awsAccessKeyId,
                    awsSecretAccessKey
                ),
                Amazon.RegionEndpoint.GetBySystemName(awsRegion)
            )
        );

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token"
            });

            options.AddSecurityRequirement(document =>
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference(
                            "Bearer",
                            document
                        ),
                        new List<string>()
                    }
                }
            );
        });
        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }

    private static string GetRequiredEnvironmentVariable(string key)
    {
        return Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException(
                $"{key} is missing."
            );
    }
    private static void LoadEnvironmentVariables()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                return;
            }

            directory = directory.Parent;
        }
    }
}