using Bogus;
using LibraryAutomation.Domain.Entities;
using LibraryAutomation.Infrastructure.Persistence;

namespace LibraryAutomation.API.Data
{
    public static class DatabaseSeeder
    {
        public static void SeedBooks(ApplicationDbContext context)
        {
            // Sisteme soruyoruz: "İçeride hiç kitap var mı?"
            if (!context.Books.Any())
            {
                // Faker nesnesini "tr" (Türkçe) dil desteğiyle başlatıyoruz
                var bookFaker = new Faker<Book>("tr")
                    // ID'yi atlıyoruz çünkü PostgreSQL otomatik verecek
                    // Başlık için rastgele ticaret kelimeleri üretiyoruz
                    .RuleFor(b => b.Title, f => f.Commerce.ProductName() + " Kitabı")
                    // Yazar için rastgele Türkçe ad-soyad
                    .RuleFor(b => b.Author, f => f.Name.FullName())
                    // ISBN için uluslararası 13 haneli barkod formatı
                    .RuleFor(b => b.ISBN, f => f.Commerce.Ean13())
                    // Stok adedi için 1 ile 30 arası rastgele bir sayı
                    .RuleFor(b => b.AvailableCopies, f => f.Random.Number(1, 30));

                // Yukarıdaki kurallara uyarak tam 100 adet kitap üret!
                var books = bookFaker.Generate(100);

                // Üretilen bu 100 kitabı veritabanına tek seferde ekle ve kaydet
                context.Books.AddRange(books);
                context.SaveChanges();
            }
        }
    }
}