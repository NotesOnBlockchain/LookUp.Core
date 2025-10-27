using LookUp.Core.DataBase;

namespace LookUp.Core.Services
{
    public class DataBaseWriterService : BackgroundService
    {
        public DataBaseWriterService(ScanChannel scanChannel, IServiceScopeFactory scopeFactory)
        {
            ScanChannel = scanChannel;
            ScopeFactory = scopeFactory;
        }

        private ScanChannel ScanChannel { get; }
        public IServiceScopeFactory ScopeFactory { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in ScanChannel.MessageChannel.Reader.ReadAllAsync(stoppingToken))
            {
                using var scope = ScopeFactory.CreateScope();
                var messageRepo = scope.ServiceProvider.GetRequiredService<MessageRepository>();

                messageRepo.AddMessage(message);
            }
        }
    }
}
