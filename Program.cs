using System.Text;
using CampGearBackend.Controllers;
using CampGearBackend.Middleware;
using CampGearBackend.Models;
using CampGearBackend.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

Env.Value = builder.Configuration;
DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CampGear API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT token from POST /api/v1/auth/login"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
});

builder.Services.Configure<DendaSettings>(builder.Configuration.GetSection("Denda"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

//CORS FOR THE FRONTEND (ORIGINS ARE CONFIGURED IN appsettings.json)
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<KategoriService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddScoped<AlatService>();
builder.Services.AddScoped<UnitAlatService>();
builder.Services.AddScoped<PeminjamanService>();
builder.Services.AddScoped<PengembalianService>();
builder.Services.AddScoped<LogAktivitasService>();
builder.Services.AddScoped<LaporanService>();

var app = builder.Build();



app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//SERVED UPLOADED IMAGES: /uploads/alat/<file>
app.UseStaticFiles();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        throw;
    }
    finally
    {
        sw.Stop();
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"{timestamp} INFO: {ip} - \"{context.Request.Method} {context.Request.Path} {context.Response.StatusCode}\" {sw.ElapsedMilliseconds}ms");
    }
});

app.MapAuth();
app.MapUsers();
app.MapKategori();
app.MapAlat();
app.MapUnitAlat();
app.MapPeminjaman();
app.MapPengembalian();
app.MapLogAktivitas();
app.MapLaporan();

app.Run();
