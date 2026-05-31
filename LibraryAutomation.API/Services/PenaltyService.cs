using LibraryAutomation.Domain.Entities;
using LibraryAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LibraryAutomation.API.Services;

namespace LibraryAutomation.API.Services
{
    public class PenaltyService
    {
        private readonly ApplicationDbContext _context;

        public PenaltyService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hangfire bu metodu tetikleyecek
        public async Task CalculatePenaltiesAsync()
        {
            // 1. Teslim tarihi geçmiş ve HALA iade edilmemiş (Aktif) ödünç işlemlerini bul
            var overdueLoans = await _context.Loans
                .Where(l => l.Status == LoanStatus.Active && l.DueDate < DateTime.UtcNow)
                .ToListAsync();

            // 2. Her bir gecikmiş işlem için cezayı hesapla
            foreach (var loan in overdueLoans)
            {
                var delayDays = (DateTime.UtcNow - loan.DueDate).Days;

                if (delayDays > 0)
                {
                    loan.PenaltyAmount = delayDays * 5; // Günlük 5 birim ceza
                }
            }

            // 3. Eğer güncellenen ceza varsa veritabanına kaydet
            if (overdueLoans.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}