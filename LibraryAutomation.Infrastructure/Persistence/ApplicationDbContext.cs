using LibraryAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryAutomation.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Veritabanındaki tablolarımız
        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Loan> Loans { get; set; }

        // Tablolar arası ilişkileri (SQL mantığını) burada yapılandırıyoruz
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Loan (Ödünç İşlemi) ve User (Kullanıcı) ilişkisi -> Bir kullanıcının birden fazla ödünç işlemi olabilir.
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.User)
                .WithMany(u => u.Loans)
                .HasForeignKey(l => l.UserId);

            // Loan (Ödünç İşlemi) ve Book (Kitap) ilişkisi -> Bir kitabın geçmişte veya şu an birden fazla ödünç işlemi olabilir.
            modelBuilder.Entity<Loan>()
                .HasOne(l => l.Book)
                .WithMany(b => b.Loans)
                .HasForeignKey(l => l.BookId);
        }
    }
}