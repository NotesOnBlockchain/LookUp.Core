using LookUp.Core.DataBase;

namespace LookUp.Core.Services
{
    public class DataBaseWriterService : BackgroundService
    {
        public DataBaseWriterService(ScanChannel scanChannel, MessageRepository messageRepository)
        {
            ScanChannel = scanChannel;
            MessageRepo = messageRepository;
        }

        private ScanChannel ScanChannel { get; }
        private MessageRepository MessageRepo { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in ScanChannel.MessageChannel.Reader.ReadAllAsync(stoppingToken))
            {
                MessageRepo.AddMessage(message);
            }
        }
    }
}
