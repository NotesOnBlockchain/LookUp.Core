using LookUp.Models;
using Microsoft.EntityFrameworkCore;

namespace LookUp.Scanner.DataBase
{
    public class MessageRepository
    {
        private const string LikeEscapeCharacter = "\\";

        private readonly MessageDatabaseContext _dBContext;

        public MessageRepository(MessageDatabaseContext databaseContext)
        {
            _dBContext = databaseContext;
        }

        public List<MessageModel> GetMessages()
        {
             return _dBContext.Messages.ToList();
        }

        public async Task<int?> GetNextBlockHeightAsync(CancellationToken cancellationToken)
        {
            var progress = await _dBContext.ScanProgress.AsNoTracking()
                .SingleOrDefaultAsync(p => p.ID == ScanProgress.SingletonID, cancellationToken);

            return progress?.NextBlockHeight;
        }

        public async Task CreateScanProgressAsync(int nextBlockHeight, CancellationToken cancellationToken)
        {
            _dBContext.ScanProgress.Add(new ScanProgress { NextBlockHeight = nextBlockHeight });
            await _dBContext.SaveChangesAsync(cancellationToken);
        }

        // Stores a block's messages and advances the scan progress past it. A single SaveChanges runs in one
        // transaction, so either both are stored or neither is, and a failure is thrown to the caller to retry the block.
        public async Task SaveBlockAsync(IReadOnlyList<MessageModel> messages, int nextBlockHeight, CancellationToken cancellationToken)
        {
            if (messages.Count > 0)
            {
                // A rescanned block or a re-mined transaction can bring messages that are already stored, and one
                // transaction can repeat the same OP_RETURN data. Inserting either would violate the (TransactionID, Hex)
                // constraint and roll back the whole block.
                var transactionIds = messages.Select(m => m.TransactionID).ToList();
                var stored = await _dBContext.Messages
                    .Where(m => transactionIds.Contains(m.TransactionID))
                    .Select(m => new { m.TransactionID, m.Hex })
                    .ToListAsync(cancellationToken);
                var storedKeys = stored.Select(s => (s.TransactionID, s.Hex)).ToHashSet();

                foreach (var message in messages)
                {
                    // Add returns false for a key that is stored or was already added from this block.
                    if (!storedKeys.Add((message.TransactionID, message.Hex)))
                    {
                        Logger.Logger.LogWarning($"Message already stored or repeated in this block, skipping. TxID: {message.TransactionID}");
                        continue;
                    }

                    _dBContext.Messages.Add(message);
                }
            }

            var progress = await _dBContext.ScanProgress.SingleAsync(p => p.ID == ScanProgress.SingletonID, cancellationToken);
            progress.NextBlockHeight = nextBlockHeight;

            await _dBContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<MessageModel>> FindAsync(string query, CancellationToken cancellationToken)
        {
            DateTimeOffset? dayStart = null;
            DateTimeOffset? dayEnd = null;

            if (DateTime.TryParse(query, out var parsed))
            {
                dayStart = new DateTimeOffset(parsed.Date, TimeSpan.Zero);
                dayEnd = dayStart.Value.AddDays(1);
            }

            // Each branch hits its own index.

            // The query is matched literally: without escaping, "%%%" or "___" would match every message.
            string messagePattern = $"%{EscapeLikePattern(query)}%";
            var combined = _dBContext.Messages
                .Where(m => EF.Functions.ILike(m.Message, messagePattern, LikeEscapeCharacter));

            // Transaction IDs, block hashes and Hex are stored as hex, so a query with any other character can't match them.
            if (query.All(char.IsAsciiHexDigit))
            {
                var byTransactionId = _dBContext.Messages
                    .Where(m => m.TransactionID == query);

                var byHex = _dBContext.Messages
                    .Where(m => m.Hex == query);

                var byBlockHash = _dBContext.Messages
                    .Where(m => m.BlockHash == query);

                combined = combined
                    .Union(byTransactionId)
                    .Union(byHex)
                    .Union(byBlockHash);
            }

            if (dayStart != null)
            {
                var byDate = _dBContext.Messages
                    .Where(m => m.BlockMinedAt >= dayStart && m.BlockMinedAt < dayEnd);

                combined = combined.Union(byDate);
            }

            return await combined
                .OrderByDescending(m => m.BlockMinedAt)
                .Take(Constants.Constants.MaxSearchResults)
                .ToListAsync(cancellationToken);

        }

        // Escapes the escape character first, so the backslashes added for % and _ aren't doubled.
        private static string EscapeLikePattern(string value) =>
            value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
                .Replace("%", LikeEscapeCharacter + "%")
                .Replace("_", LikeEscapeCharacter + "_");
    }
}
