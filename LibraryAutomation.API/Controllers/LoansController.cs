using LibraryAutomation.Domain.Entities;
using LibraryAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace LibraryAutomation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // KAPIYI KİLİTLEDİK: Sadece Token'ı olanlar bu sınıfa girebilir!
    public class LoansController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache; // REDIS İÇERİ ALINDI

        public LoansController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // POST: api/loans/borrow/{bookId}
        [HttpPost("borrow/{bookId}")]
        public async Task<IActionResult> BorrowBook(int bookId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            }

            var book = await _context.Books.FindAsync(bookId);
            var user = await _context.Users.FindAsync(userId);
            var alreadyBorrowed = await _context.Loans.AnyAsync(l =>
                l.UserId == userId &&
                l.BookId == bookId &&
                l.Status == LoanStatus.Active);

            if (alreadyBorrowed)
            {
                return BadRequest("Bu kitabı zaten ödünç aldınız ve henüz iade etmediniz.");
            }

            if (book == null) return NotFound("Kitap bulunamadı.");
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            if (book.AvailableCopies <= 0)
            {
                return BadRequest("Bu kitabın tüm kopyaları şu an ödünç verilmiş durumda.");
            }

            var loan = new Loan
            {
                UserId = userId,
                BookId = bookId,
                BorrowDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(15),
                Status = LoanStatus.Active
            };

            book.AvailableCopies -= 1;

            await _context.Loans.AddAsync(loan);
            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("all_booksList");

            return Ok(new { Mesaj = "Kitap başarıyla ödünç verildi", TeslimTarihi = loan.DueDate });
        }

        // POST: api/loans/return/{id}
        [HttpPost("return/{id}")]
        public async Task<IActionResult> ReturnBook(int id)
        {
            var loan = await _context.Loans
                .Include(l => l.Book)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null) return NotFound("Ödünç işlemi bulunamadı.");

            if (loan.Status == LoanStatus.Returned)
                return BadRequest("Bu kitap zaten iade edilmiş.");

            loan.ReturnDate = DateTime.UtcNow;
            loan.Status = LoanStatus.Returned;

            if (loan.ReturnDate > loan.DueDate)
            {
                var delayDays = (loan.ReturnDate.Value - loan.DueDate).Days;
                loan.PenaltyAmount = delayDays * 5;
            }

            if (loan.Book != null)
            {
                loan.Book.AvailableCopies += 1;
            }

            await _context.SaveChangesAsync();

            await _cache.RemoveAsync("all_booksList");

            return Ok(new
            {
                Mesaj = "Kitap başarıyla iade alındı.",
                GecikilenGun = loan.PenaltyAmount / 5,
                KesilenCeza = loan.PenaltyAmount
            });
        }

        // GET: api/loans/my-loans
        [HttpGet("my-loans")]
        public async Task<IActionResult> GetMyLoans()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Kullanıcı kimliği doğrulanamadı.");
            }

            var userLoans = await _context.Loans
                .Include(l => l.Book)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.BorrowDate)
                .Select(l => new
                {
                    id = l.Id,
                    book = new
                    {
                        id = l.Book.Id,
                        title = l.Book.Title,
                        author = l.Book.Author,
                        coverImageUrl = "" 
                    },
                    borrowDate = l.BorrowDate,
                    dueDate = l.DueDate,
                    returnDate = l.ReturnDate,
                    status = l.Status.ToString(),
                    penaltyAmount = l.PenaltyAmount
                })
                .ToListAsync();

            return Ok(userLoans);
        }
    }
}