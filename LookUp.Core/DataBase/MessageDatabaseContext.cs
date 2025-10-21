using LookUp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Core.DataBase
{
    public class MessageDatabaseContext : DbContext
    {
        public MessageDatabaseContext(DbContextOptions options) : base(options) 
        {
        }
        public DbSet<MessageModel> Messages { get; set; }
    }
}
