using TF.Data;
using Microsoft.EntityFrameworkCore;
using TF.Services;
using TF.Extensions;
using TF.ViewModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<TFDataContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()
    )
);

// Services
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddTransient<TokenService>();
builder.Services.AddTransient<AccountService>();
builder.Services.AddTransient<Trust_Finance.Services.CategoryService>();
builder.Services.AddTransient<Trust_Finance.Services.TransactionService>();

// CORS (SPA dev server)
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// [ApiController] rejects invalid models before the action ever runs, so validation
// failures have to be shaped here. Without this they come back as RFC 7807
// ProblemDetails while every other error uses the ResultViewModel envelope.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new ResultViewModel<string>(context.ModelState.GetErrors()));
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TF API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT com o prefixo 'Bearer'"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// JWT key validation (removes CS8604)
var jwtKey = builder.Configuration["JwtKey"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JwtKey is not configured. Set it in appsettings or as an environment variable.");

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.ASCII.GetBytes(jwtKey)
        ),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Top-level statements compile into an internal Program class. Making it public
// lets WebApplicationFactory<Program> boot this exact pipeline in integration tests.
public partial class Program { }
