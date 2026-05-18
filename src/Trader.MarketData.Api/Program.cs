using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Trader.MarketData.Data;
using Trader.MarketData.Api.Grpc;
using Trader.MarketData.Api.Services;
using Trader.Providers.PortfolioPersonal;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────────
var marketDataConnStr = builder.Configuration.GetConnectionString("MarketData")
    ?? throw new InvalidOperationException("Missing connection string 'MarketData'.");
var dbPassword = Environment.GetEnvironmentVariable("TRADER_QUOTAS_DB_PWD")
    ?? throw new InvalidOperationException("Missing environment variable 'TRADER_QUOTAS_DB_PWD'.");
marketDataConnStr += $";Password={dbPassword}";
builder.Services.AddMarketDataDb(marketDataConnStr);

// ─── Provider ─────────────────────────────────────────────────────────────────
builder.Services.AddPortfolioPersonalProviders(
    builder.Configuration.GetSection("PortfolioPersonal"));

// ─── Application services ─────────────────────────────────────────────────────
builder.Services.AddScoped<QuoteQueryService>();

// ─── Authentication ───────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Missing Jwt:Key configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ─── gRPC ─────────────────────────────────────────────────────────────────────
builder.Services.AddGrpc();

// ─── REST ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Trader MarketData API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Example: 'Bearer {token}'",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<QuoteGrpcService>();
app.MapControllers();

app.Run();
