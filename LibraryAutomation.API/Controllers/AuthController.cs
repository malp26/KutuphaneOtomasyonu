using LibraryAutomation.Domain.Entities;
using LibraryAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LibraryAutomation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.PasswordHash != request.Password)
            {
                return Unauthorized("Geçersiz e-posta veya şifre.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var keyString = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key bulunamadı!");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return Ok(new
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Mesaj = "Giriş başarılı!"
            });
        }

        // YENİ EKLENEN KAYIT (REGISTER) UCU
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 1. Bu e-posta adresiyle daha önce kayıt olunmuş mu?
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (emailExists)
            {
                // 400 Bad Request dönüyoruz, arayüzdeki toast.error bunu yakalayacak
                return BadRequest(new { message = "Bu e-posta adresi zaten kullanımda." });
            }

            // 2. Yeni kullanıcıyı oluştur
            // Not: Login metodunda PasswordHash alanına düz metin olarak "12345" gibi baktığımız için 
            // şimdilik şifreyi şifrelemeden (hash'lemeden) düz kaydediyoruz ki Login ile eşleşsin.
            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = request.Password,
                // Eğer User modelinde Ad-Soyad veya Rol tuttuğun property'ler varsa onları da aşağıya ekleyebilirsin:
                // Name = request.Name,
                // Role = Role.Member 
            };

            // 3. Veritabanına kaydet
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 4. React tarafı kayıt başarılı olunca direkt /login sayfasına yönlendirecek
            return Ok(new { message = "Kayıt işlemi başarılı. Lütfen giriş yapın." });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // YENİ EKLENEN MODEL: React arayüzündeki { name, email, password } isteğini karşılar
    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}   