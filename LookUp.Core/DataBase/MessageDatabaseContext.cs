using LookUp.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Scanner.DataBase
{
    public class MessageDatabaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<MessageModel> Messages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MessageModel>().HasAlternateKey(x => new { x.TransactionID, x.Hex });
        }
    }
}
