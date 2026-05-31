using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryAutomation.Domain.Entities
{
    public enum LoanStatus
    {
        Active = 1,     // Şu an kullanıcıda
        Returned = 2,   // İade edildi
        Overdue = 3     // Gecikmiş
    }

    public class Loan
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        public DateTime BorrowDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        public LoanStatus Status { get; set; } = LoanStatus.Active;

        public decimal PenaltyAmount { get; set; } = 0;
    }
}
