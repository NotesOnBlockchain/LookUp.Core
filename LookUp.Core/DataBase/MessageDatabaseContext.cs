using LookUp.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Scanner.DataBase
{
    public class MessageDatabaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<MessageModel> Messages { get; set; }

        public DbSet<ScanProgress> ScanProgress { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MessageModel>().HasAlternateKey(x => new { x.TransactionID, x.Hex });

            modelBuilder.Entity<MessageModel>()
                .HasIndex(x => x.BlockHash);

            modelBuilder.Entity<MessageModel>()
                .HasIndex(x => x.BlockMinedAt);

            // Hex is the second column of the (TransactionID, Hex) key, so that index can't serve a lookup by Hex alone.
            modelBuilder.Entity<MessageModel>()
                .HasIndex(x => x.Hex);

            modelBuilder.Entity<ScanProgress>()
                .Property(x => x.ID)
                .ValueGeneratedNever();

        }
    }
}
