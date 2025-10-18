using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Keytietkiem Admin API",
        Version = "v1",
        Description = "Audit Logs & Admin APIs"
    });


    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nh?p JWT theo d?ng: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

builder.Services.AddDbContext<KeytietkiemContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("Audit.Read", p => p.RequireClaim("perm", "Audit.Read"));
    opt.AddPolicy("Audit.Export", p => p.RequireClaim("perm", "Audit.Export"));
    opt.AddPolicy("Audit.ViewSensitive", p => p.RequireClaim("perm", "Audit.ViewSensitive"));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Keytietkiem Admin API v1");
    c.DocumentTitle = "Keytietkiem Swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
