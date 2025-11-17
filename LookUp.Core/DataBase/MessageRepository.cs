using LookUp.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Scanner.DataBase
{
    public class MessageRepository
    {
        private readonly MessageDatabaseContext _dBContext;

        public MessageRepository(MessageDatabaseContext databaseContext)
        {
            _dBContext = databaseContext;
        }

        public List<MessageModel> GetMessages()
        {
             return _dBContext.Messages.ToList();
        }

        public void AddMessage(MessageModel message)
        {
            try
            {
                _dBContext.Messages.Add(message);
                _dBContext.SaveChanges();
            }
            catch (DbUpdateException ex) when (ex.InnerException!.Message.Contains("duplicate key value"))
            {
                Logger.Logger.LogWarning(ex);
            }
            catch (Exception ex) 
            {
                Logger.Logger.LogCritical(ex);
            }
        }

        public async Task<List<MessageModel>> FindAsync(string query)
        {
            DateTimeOffset? dayStart = null;
            DateTimeOffset? dayEnd = null;

            if (DateTime.TryParse(query, out var parsed))
            {
                dayStart = new DateTimeOffset(parsed.Date, TimeSpan.Zero);
                dayEnd = dayStart.Value.AddDays(1);
            }

            return await _dBContext.Messages.Where(model =>
                model.TransactionID.Equals(query) ||
                EF.Functions.ILike(model.Message, $"%{query}%") ||
                model.Hex.Equals(query) ||
                model.BlockHash.Equals(query) ||
                (dayStart != null && model.BlockMinedAt >= dayStart && dayEnd != null && model.BlockMinedAt < dayEnd))
                .Distinct()
                .ToListAsync();
            
        }
    }
}
