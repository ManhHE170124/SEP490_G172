using System.Text.Json;
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers: JSON camelCase để khớp FE
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Chuẩn hóa lỗi validate thành { message: "..." }
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

// DbContext
builder.Services.AddDbContext<KeytietkiemDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("MyCnn");
    opt.UseSqlServer(conn);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS cho React dev server
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("fe", p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithOrigins("http://localhost:3000", "https://localhost:3000"));
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Global exception → { message: "..."}
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

app.UseHttpsRedirection();
app.UseCors("fe");
app.UseAuthorization();

app.MapControllers();
app.Run();
