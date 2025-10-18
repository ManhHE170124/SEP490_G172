using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

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

        // Enable CORS for React (localhost:3000)
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

        var app = builder.Build();

        // ❌ Không bật HTTPS redirect để tránh chuyển hướng
        // app.UseHttpsRedirection();

        // Enable CORS
        app.UseCors("AllowReactApp");

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
