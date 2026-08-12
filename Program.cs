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
using System.Text;

public partial class Program
{
    private static void Main(string[] args)
    {
        LoadEnvironmentVariables();

        var awsAccessKeyId = Environment.GetEnvironmentVariable("ACCESS_KEY_ID")
            ?? throw new InvalidOperationException("ACCESS_KEY_ID is missing.");
        var awsSecretAccessKey = Environment.GetEnvironmentVariable("SECRET_ACCESS_KEY")
            ?? throw new InvalidOperationException("SECRET_ACCESS_KEY is missing.");
        var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION")
            ?? throw new InvalidOperationException("AWS_REGION is missing.");

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")
            )
        );

        // JWT Authentication
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

                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],

                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]!
                        )
                    )
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddScoped<JwtService>();
        builder.Services.AddEndpointsApiExplorer();

        // Swagger + JWT
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
                        new OpenApiSecuritySchemeReference("Bearer", document),
                        new List<string>()
                    }
                });
        });

        builder.Services.AddScoped<JwtService>();
        builder.Services.AddScoped<RecordHelper>();
        builder.Services.AddScoped<DbSaveHelper>();
        builder.Services.AddScoped<RecordMapperHelper>();
        builder.Services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(
                new BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey),
                Amazon.RegionEndpoint.GetBySystemName(awsRegion)));
        builder.Services.AddScoped<S3PresignedUrlService>();

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
