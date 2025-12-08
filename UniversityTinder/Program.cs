using AutoMapper;
using Microsoft.EntityFrameworkCore;
using UniversityTinder.Data;
using UniversityTinder;
using Serilog;
using UniversityTinder.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using UniversityTinder.Extensions;
using UniversityTinder.Services.IServices;
using UniversityTinder.Services;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Amazon.SQS;
using Amazon.Rekognition;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// SERILOG CONFIGURATION
// ============================================
builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq("http://localhost:5341");
});

// ============================================
// DATABASE CONFIGURATION
// ============================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultSQLConnection")));

// ============================================
// AUTOMAPPER CONFIGURATION
// ============================================
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
builder.Services.AddSingleton(mapper);
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// ============================================
// JWT CONFIGURATION
// ============================================
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("ApiSettings:JwtOptions"));

// ============================================
// AWS CONFIGURATION
// ============================================
var awsOptions = new AWSOptions
{
    Credentials = new Amazon.Runtime.BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"]),
    Region = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
};

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonSQS>();
builder.Services.AddAWSService<IAmazonRekognition>();

// ============================================
// IDENTITY CONFIGURATION
// ============================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ============================================
// CONTROLLERS & INFRASTRUCTURE
// ============================================
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();  // ✅ ImageService için gerekli

// ============================================
// APPLICATION SERVICES
// ============================================
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IFaceVerificationService, AwsRekognitionFaceVerificationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IImageSensorService, ImageSensorService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISwipeService, SwipeService>();
builder.Services.AddScoped<IPasswordResetCodeService, PasswordResetCodeService>();
builder.Services.AddScoped<IEmailVerificationCodeService, EmailVerificationCodeService>();
builder.Services.AddScoped<IUniversitySeedService, UniversitySeedService>();

// ============================================
// SWAGGER CONFIGURATION
// ============================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.AddSecurityDefinition(
        name: JwtBearerDefaults.AuthenticationScheme,
        securityScheme: new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter the Bearer Authorization string as following: 'Bearer Generated-JWT-Token'",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            new string[] { }
        }
    });
});

// ============================================
// AUTHENTICATION & AUTHORIZATION
// ============================================
builder.AddAppAuthetication();
builder.Services.AddAuthorization();

// ============================================
// CORS CONFIGURATION
// ============================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", corsBuilder =>
    {
        corsBuilder.WithOrigins(
                "https://universitytinderv2.justkey.online/",
                "https://universitytinder.justkey.online/",
                "http://localhost:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ============================================
// BUILD APPLICATION
// ============================================
var app = builder.Build();

// ============================================
// MIDDLEWARE PIPELINE
// ============================================

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UniversityTinder API v1");
    if (!app.Environment.IsDevelopment())
    {
        c.RoutePrefix = "swagger";
    }
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();

// Static files (wwwroot varsa)
if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")))
{
    app.UseStaticFiles();
}

app.MapControllers();

// ============================================
// STARTUP LOG
// ============================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 UniversityTinder API Started");
logger.LogInformation("📍 Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("🌍 AWS Region: {Region}", builder.Configuration["AWS:Region"]);
logger.LogInformation("💾 Database: Connected");

app.Run();