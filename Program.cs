using System.Text.Json.Serialization;
using System.Text;
using EmployeeLeaveApi.Data;
using EmployeeLeaveApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ITokenService, TokenService>();

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
    });
builder.Services.AddAuthorization();

// CORS: allowed origins come from config (Cors:AllowedOrigins as an array or a comma-separated string).
var corsSection = builder.Configuration.GetSection("Cors:AllowedOrigins");
var allowedOrigins = corsSection.GetChildren().Select(c => c.Value)
    .Concat((corsSection.Value ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o!.TrimEnd('/'))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
if (allowedOrigins.Count == 0) allowedOrigins.Add("http://localhost:4200");
var allowVercelPreviews = builder.Configuration.GetValue<bool>("Cors:AllowVercelPreviews");
builder.Services.AddCors(o => o.AddPolicy("AllowAngular", p =>
    p.SetIsOriginAllowed(origin =>
        allowedOrigins.Contains(origin.TrimEnd('/')) ||
        (allowVercelPreviews && Uri.TryCreate(origin, UriKind.Absolute, out var u) && u.Scheme == "https"
            && u.Host.StartsWith("hr-portal-angular", StringComparison.OrdinalIgnoreCase)
            && u.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)))
     .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EmployeeLeaveApi", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT token (without 'Bearer ')."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Configuration.GetValue<bool>("DemoMode"));
}

// Swagger is enabled in every environment so the live demo exposes /swagger.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads", "avatars"));
app.UseStaticFiles();
app.UseCors("AllowAngular");

// Public demo: destructive actions and password resets are disabled (DemoMode=false locally).
if (app.Configuration.GetValue<bool>("DemoMode"))
{
    app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path.Value ?? "";
        if (HttpMethods.IsDelete(ctx.Request.Method)
            || path.StartsWith("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { message = "Disabled in demo" });
            return;
        }
        await next();
    });
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
