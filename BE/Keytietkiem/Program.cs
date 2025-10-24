using System.Text.Json;
using System.Text.Json.Serialization;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== DI =====

builder.Services.AddDbContextFactory<KeytietkiemDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Clock (để test dễ mock thời gian)
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

const string FrontendCors = "Frontend";
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                 ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p =>
    p.WithOrigins(corsOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===== Middleware pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // vẫn có thể bật Swagger ngoài prod nếu muốn
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCors);

app.MapControllers();

app.Run();
