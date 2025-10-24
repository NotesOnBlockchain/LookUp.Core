using LookUp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Core.DataBase
{
    public class MessageDatabaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<MessageModel> Messages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

        }
    }
}
