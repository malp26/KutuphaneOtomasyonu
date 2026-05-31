using Hangfire;
using Hangfire.PostgreSql;
using LibraryAutomation.API.Services;
using LibraryAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models; // OpenApi hatalarını çözen kütüphane
using System.Text;
using LibraryAutomation.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Null hatasını çözen güvenlik garantisi: "Eğer Key yoksa sistemi güvenli bir şekilde durdur"
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key appsettings.json dosyasında bulunamadı!");

// 2. JWT Kimlik Doğrulama Ayarları
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
            // Burada artık güvenli olan jwtKey değişkenini kullanıyoruz
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"));
    });
});
builder.Services.AddHangfireServer();
// Redis Önbellek (Cache) Servisi Entegrasyonu
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Docker üzerinden ayağa kaldırdığımız Redis'in adresi
    options.Configuration = "localhost:6379";
    // Cache'de tutulan verilerin isminin başına eklenecek ön ek (Karışıklığı önler)
    options.InstanceName = "LibraryApp_";
});
// CORS Politikası: Dış dünyadan (Frontend) gelecek isteklere izin veriyoruz
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()   // Herhangi bir adresten (domainden) gelen isteği kabul et
              .AllowAnyHeader()   // Gelen isteğin içindeki başlıklara (Token vb.) izin ver
              .AllowAnyMethod();  // Tüm metotlara (GET, POST, PUT, DELETE) izin ver
    });
});

// 3. Swagger'a JWT "Authorize" Butonunu Ekleme
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Lütfen token'ınızı 'Bearer {token}' formatında giriniz.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    // Eğer bekleyen Migration varsa önce onları veritabanına uygular (Veritabanı yoksa oluşturur)
    context.Database.Migrate();

    // Ve tohumlama metodumuzu çağırıyoruz
    DatabaseSeeder.SeedBooks(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHangfireDashboard();
app.UseHttpsRedirection();
app.UseHttpsRedirection();

// CORS POLITIKASINI DEVREYE AL (Kimlik kontrolünden hemen önce olmalı!)
app.UseCors("AllowAll");

app.UseAuthentication(); // Sen kimsin?
app.UseAuthorization();  // İznin var mı?


// 4. Kapıdaki Güvenlik Görevlilerini Aktif Etme
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
RecurringJob.AddOrUpdate<PenaltyService>(
    "otomatik-ceza-hesaplama-robotu",
    service => service.CalculatePenaltiesAsync(),
    Cron.Daily(0,0));
app.Run();