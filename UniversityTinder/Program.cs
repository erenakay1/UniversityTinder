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

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

// Serilog'u Seq ile yapýlandýr
builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration) // appsettings.json'dan okur
        .Enrich.FromLogContext()
        .WriteTo.Seq("http://localhost:5341"); // Seq sunucusunun URL'si
});


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultSQLConnection")));
IMapper mapper = MappingConfig.RegisterMaps().CreateMapper();
builder.Services.AddSingleton(mapper);


builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("ApiSettings:JwtOptions"));
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
var awsOptions = new AWSOptions
{
    Credentials = new Amazon.Runtime.BasicAWSCredentials(
        builder.Configuration["AWS:AccessKey"],
        builder.Configuration["AWS:SecretKey"]),
    Region = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"])
};
builder.Services.AddAWSService<IAmazonS3>(awsOptions);
builder.Services.AddAWSService<IAmazonSQS>(awsOptions);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IImageSensorService, ImageSensorService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordResetCodeService, PasswordResetCodeService>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.AddSecurityDefinition(name: JwtBearerDefaults.AuthenticationScheme, securityScheme: new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter the Bearer Authorization string as following: 'Bearer Generated-JWT-Token'",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },new string[] { }
        }
    });
});
builder.AddAppAuthetication();
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", builder =>
    {
        builder.WithOrigins("https://apiv2.paficdev.com/", "https://api.paficdev.com/")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers();

app.Run();
