using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ===== Connection string =====
var connStr = builder.Configuration.GetConnectionString("MyCnn");

// ===== DI (ưu tiên bản dưới) =====
// Dùng DbContextFactory để dễ test và control scope
builder.Services.AddDbContextFactory<KeytietkiemDbContext>(opt =>
    opt.UseSqlServer(connStr));

// ===== Configuration Options =====
builder.Services.Configure<MailConfig>(builder.Configuration.GetSection("MailConfig"));
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("JwtConfig"));
builder.Services.Configure<ClientConfig>(builder.Configuration.GetSection("ClientConfig"));

// ===== Memory Cache =====
builder.Services.AddMemoryCache();

// ===== Repositories =====
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// ===== Services =====
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPhotoService, CloudinaryService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ILicensePackageService, LicensePackageService>();
builder.Services.AddScoped<IProductKeyService, ProductKeyService>();
builder.Services.AddScoped<IProductAccountService, ProductAccountService>();
builder.Services.AddScoped<IWebsiteSettingService, WebsiteSettingService>();
builder.Services.AddScoped<ILayoutSectionService, LayoutSectionService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();

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

// ===== SignalR cho realtime Ticket chat =====
builder.Services.AddSignalR();

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

// ===== JWT Authentication =====
var jwtSecretKey = builder.Configuration["JwtConfig:SecretKey"]
                   ?? throw new InvalidOperationException("JwtConfig:SecretKey not found in appsettings.json");
var jwtIssuer = builder.Configuration["JwtConfig:Issuer"]
                ?? throw new InvalidOperationException("JwtConfig:Issuer not found in appsettings.json");
var jwtAudience = builder.Configuration["JwtConfig:Audience"]
                  ?? throw new InvalidOperationException("JwtConfig:Audience not found in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var app = builder.Build();

// ===== Seed default roles at startup =====
using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
    await roleService.SeedDefaultRolesAsync();
    var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
    await accountService.SeedDataAsync();
}

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
app.UseAuthentication();
app.UseAuthorization();

// ===== Endpoint mapping =====
app.MapControllers();

// Hub realtime cho ticket chat (chỉ dùng cho khung chat)
app.MapHub<TicketHub>("/hubs/tickets");
app.MapHub<SupportChatHub>("/hubs/support-chat");


app.Run();
