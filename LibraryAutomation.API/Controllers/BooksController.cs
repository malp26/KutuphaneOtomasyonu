using LibraryAutomation.Domain.Entities;
using LibraryAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LibraryAutomation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        // Dependency Injection: Veritabanı bağlamımızı (DbContext) ve Redis'i içeri alıyoruz
        public BooksController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // GET: api/books
        // Tüm kitapları listeleyen asenkron uç nokta (Redis Cache Destekli)
        [AllowAnonymous] // Herkese açık (Ziyaretçiler arayüzden görebilir)
        [HttpGet]
        public async Task<IActionResult> GetAllBooks()
        {
            var cacheKey = "all_booksList";
            string? cachedBooks = await _cache.GetStringAsync(cacheKey);

            // 1. Durum: Veri Redis'te (RAM) varsa, veritabanına hiç gitmeden doğrudan yolla!
            if (!string.IsNullOrEmpty(cachedBooks))
            {
                var booksFromCache = JsonSerializer.Deserialize<List<Book>>(cachedBooks);
                return Ok(new { Kaynak = "Redis Cache (Çok Hızlı)", Kitaplar = booksFromCache });
            }

            // 2. Durum: Veri Redis'te yoksa PostgreSQL'e git
            var booksFromDb = await _context.Books.ToListAsync();

            // Veritabanından gelen veriyi JSON metnine çevirip Redis'e kaydet (10 dakika boyunca tut)
            var serializedBooks = JsonSerializer.Serialize(booksFromDb);
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

            await _cache.SetStringAsync(cacheKey, serializedBooks, cacheOptions);

            return Ok(new { Kaynak = "PostgreSQL Veritabanı", Kitaplar = booksFromDb });
        }

        // POST: api/books
        // Yeni kitap ekleyen asenkron uç nokta
        [Authorize(Roles = "Admin")] // SADECE ADMİN
        [HttpPost]
        public async Task<IActionResult> AddBook([FromBody] Book book)
        {
            if (book == null)
            {
                return BadRequest("Geçersiz kitap verisi.");
            }

            await _context.Books.AddAsync(book);
            await _context.SaveChangesAsync();

            // YENİ EKLEME: Kitap listesi değiştiği için Redis'teki eski listeyi çöpe atıyoruz
            await _cache.RemoveAsync("all_booksList");

            return Ok(new { Mesaj = "Kitap başarıyla kütüphaneye eklendi.", Kitap = book });
        }

        // PUT: api/books/{id}
        // Mevcut bir kitabı güncelleyen asenkron uç nokta
        [Authorize(Roles = "Admin")] // SADECE ADMİN
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(int id, [FromBody] Book updatedBook)
        {
            if (id != updatedBook.Id)
            {
                return BadRequest("URL'deki ID ile kitabın ID'si eşleşmiyor.");
            }

            var existingBook = await _context.Books.FindAsync(id);
            if (existingBook == null)
            {
                return NotFound("Güncellenecek kitap bulunamadı.");
            }

            // Arayüzden gelen yeni verileri mevcut kitaba aktarıyoruz
            existingBook.Title = updatedBook.Title;
            existingBook.Author = updatedBook.Author;
            existingBook.ISBN = updatedBook.ISBN;
            existingBook.AvailableCopies = updatedBook.AvailableCopies;

            await _context.SaveChangesAsync();

            // YENİ EKLEME: Kitap listesi değiştiği için Redis'teki eski listeyi çöpe atıyoruz
            await _cache.RemoveAsync("all_booksList");

            return Ok(new { Mesaj = "Kitap başarıyla güncellendi.", Kitap = existingBook });
        }

        // DELETE: api/books/{id}
        // Mevcut bir kitabı silen asenkron uç nokta
        [Authorize(Roles = "Admin")] // SADECE ADMİN
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound("Silinecek kitap bulunamadı.");
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            // YENİ EKLEME: Kitap listesi değiştiği için Redis'teki eski listeyi çöpe atıyoruz
            await _cache.RemoveAsync("all_booksList");

            return Ok(new { Mesaj = "Kitap sistemden tamamen silindi." });
        }
    }
}