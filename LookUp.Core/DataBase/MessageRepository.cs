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

            // Each branch hits its own index
            var byTransactionId = _dBContext.Messages
                .Where(m => m.TransactionID == query);

            var byHex = _dBContext.Messages
                .Where(m => m.Hex == query);

            var byBlockHash = _dBContext.Messages
                .Where(m => m.BlockHash == query);

            var byMessage = _dBContext.Messages
                .Where(m => EF.Functions.ILike(m.Message, $"%{query}%"));

            var combined = byTransactionId
                .Union(byHex)
                .Union(byBlockHash)
                .Union(byMessage);

            if (dayStart != null)
            {
                var byDate = _dBContext.Messages
                    .Where(m => m.BlockMinedAt >= dayStart && m.BlockMinedAt < dayEnd);

                combined = combined.Union(byDate);
            }

            return await combined
                .OrderByDescending(m => m.BlockMinedAt)
                .Take(20)
                .ToListAsync();

        }
    }
}
