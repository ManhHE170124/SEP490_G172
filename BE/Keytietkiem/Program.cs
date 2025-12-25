using Keytietkiem.Authorization;
using Keytietkiem.Hubs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Keytietkiem.Services.Background;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

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
builder.Services.Configure<SendPulseConfig>(builder.Configuration.GetSection("SendPulse"));

// ===== Memory Cache =====
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PayOSService>();

// ===== Repositories =====
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// ===== Services =====
builder.Services.AddHttpClient<ISendPulseService, SendPulseService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPhotoService, CloudinaryService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ILicensePackageService, LicensePackageService>();
builder.Services.AddScoped<IProductKeyService, ProductKeyService>();
builder.Services.AddScoped<IProductAccountService, ProductAccountService>();
builder.Services.AddScoped<IProductReportService, ProductReportService>();
builder.Services.AddScoped<IWebsiteSettingService, WebsiteSettingService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
builder.Services.AddScoped<IRealtimeDatabaseUpdateService, RealtimeDatabaseUpdateService>();  // ✅ chỉ 1 lần
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<ISupportStatsUpdateService, SupportStatsUpdateService>();          // ✅ chỉ 1 lần
builder.Services.AddHostedService<CartCleanupService>();
builder.Services.AddHostedService<PaymentTimeoutService>();
builder.Services.AddScoped<IInventoryReservationService, InventoryReservationService>();
builder.Services.AddScoped<IBannerService, BannerService>();



// Clock (mockable for tests) – dùng luôn block này
builder.Services.AddSingleton<IClock, SystemClock>();                                         // ✅ chỉ 1 lần

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

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("CartPolicy", context =>
    {
        var http = context.Request.HttpContext;

        var remoteIp = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // logged-in => key theo UserId
        var userId =
            http.User?.FindFirstValue("uid") ??
            http.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        string identityKey;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            identityKey = "u_" + userId;
        }
        else
        {
            // guest => anonId từ cookie, fallback header
            http.Request.Cookies.TryGetValue("ktk_anon_id", out var anonId);
            if (string.IsNullOrWhiteSpace(anonId))
            {
                anonId = http.Request.Headers["X-Guest-Cart-Id"].FirstOrDefault();
            }
            identityKey = "g_" + (anonId ?? "unknown");
        }

        var key = $"{remoteIp}_{identityKey}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ===== Authorization with Role-based system =====
builder.Services.AddAuthorization(options =>
{
    // Configure default policy if needed
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Register RolePolicyProvider to handle RequireRole attribute
builder.Services.AddSingleton<IAuthorizationPolicyProvider, RolePolicyProvider>();

// Register RoleAuthorizationHandler to check roles
builder.Services.AddScoped<IAuthorizationHandler, RoleAuthorizationHandler>();

// ===== Stats service + background job =====
// (ISupportStatsUpdateService đã đăng ký phía trên => KHÔNG lặp lại)
// ✅ Job thống kê support hằng ngày
//builder.Services.AddSingleton<IBackgroundJob, SupportStatsBackgroundJob>();


// ✅ Job SLA ticket mỗi 5 phút
builder.Services.AddSingleton<IBackgroundJob, TicketSlaBackgroundJob>();

// ✅ Job cập nhật trạng thái hết hạn cho ProductAccount và ProductKey (mỗi 6h)
builder.Services.AddSingleton<IBackgroundJob, ExpiryStatusUpdateJob>();


// Scheduler nhận IEnumerable<IBackgroundJob> và chạy từng job theo Interval
builder.Services.AddHostedService<BackgroundJobScheduler>();

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===== Seed default roles at startup =====
using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
    await roleService.SeedDefaultRolesAsync();
    var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
    await accountService.SeedDataAsync();
    // RolePermissionInitializer removed - permissions are now seeded via SQL script
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

// ===== Status code pages for consistent JSON (401/403) =====
app.UseStatusCodePages(async context =>
{
    var statusCode = context.HttpContext.Response.StatusCode;
    if (statusCode == StatusCodes.Status401Unauthorized ||
        statusCode == StatusCodes.Status403Forbidden)
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        var message = statusCode == StatusCodes.Status401Unauthorized
            ? "Bạn chưa đăng nhập hoặc phiên đã hết hạn."
            : "Bạn không có quyền truy cập chức năng này.";
        var payload = JsonSerializer.Serialize(new { message });
        await context.HttpContext.Response.WriteAsync(payload);
    }
});

// Theo bản dưới: tắt redirect HTTPS trong môi trường dev để tránh CORS redirect
// app.UseHttpsRedirection();

// // (Tuỳ chọn) Auth
// app.UseAuthentication();

app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
// ===== Endpoint mapping =====
app.MapControllers();

// Hub realtime cho ticket chat (chỉ dùng cho khung chat)
app.MapHub<TicketHub>("/hubs/tickets");
app.MapHub<SupportChatHub>("/hubs/support-chat");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
