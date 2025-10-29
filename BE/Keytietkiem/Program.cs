using System.Text.Json;
using System.Text.Json.Serialization;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== Connection string =====
var connStr = builder.Configuration.GetConnectionString("MyCnn");

// ===== DI (ưu tiên bản dưới) =====
// Dùng DbContextFactory để dễ test và control scope
builder.Services.AddDbContextFactory<KeytietkiemDbContext>(opt =>
    opt.UseSqlServer(connStr));

// Clock (mockable for tests)
builder.Services.AddSingleton<IClock, SystemClock>();

// ===== Controllers + JSON (ưu tiên bản dưới, có Enum -> string) =====
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Nếu dùng DateOnly/TimeOnly => thêm converter custom ở đây
        // o.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });

// ===== Uniform ModelState error => { message: "..." } (giữ nguyên) =====
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var first = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .Select(kv => kv.Value!.Errors[0].ErrorMessage)
            .FirstOrDefault() ?? "Dữ liệu không hợp lệ";
        return new BadRequestObjectResult(new { message = first });
    };
});

// ===== CORS (gộp: config + 5173 + 3000) =====
const string FrontendCors = "Frontend";
var cfgOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
var defaultOrigins = new[] { "http://localhost:5173", "http://localhost:3000", "https://localhost:3000" };
var corsOrigins = cfgOrigins.Union(defaultOrigins).Distinct().ToArray();

builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p =>
    p.WithOrigins(corsOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
     .WithExposedHeaders("Content-Disposition") // phục vụ export CSV
));

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===== Global exception -> { message: "..." } (giữ bản dưới) =====
app.UseExceptionHandler(exApp =>
{
    exApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = JsonSerializer.Serialize(new { message = "Đã có lỗi hệ thống. Vui lòng thử lại sau." });
        await context.Response.WriteAsync(payload);
    });
});

// ===== Dev tools =====
app.UseSwagger();
app.UseSwaggerUI();

// Theo bản dưới: tắt redirect HTTPS trong môi trường dev để tránh CORS redirect
// app.UseHttpsRedirection();

// // (Tuỳ chọn) Auth
// app.UseAuthentication();

app.UseCors(FrontendCors);
app.UseAuthorization();

app.MapControllers();

app.Run();
