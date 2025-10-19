using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add controllers
        builder.Services.AddControllers();

        // Connect to SQL Server
        builder.Services.AddDbContext<KeytietkiemContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("MyCnn")));

        // Enable CORS for React
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowReactApp", policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        // ✅ Add Swagger service
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Keytietkiem API",
                Version = "v1",
                Description = "Backend API for React user management frontend"
            });
        });

        var app = builder.Build();

        // ✅ Development environment: enable Swagger
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Keytietkiem API v1");
                c.RoutePrefix = "swagger"; // URL: /swagger
            });
        }

        // ❌ Không bật HTTPS redirection để tránh chuyển hướng
        // app.UseHttpsRedirection();

        app.UseCors("AllowReactApp");
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
