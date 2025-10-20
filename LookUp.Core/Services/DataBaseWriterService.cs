using LookUp.Core.DataBase;

namespace LookUp.Core.Services
{
    public class DataBaseWriterService : BackgroundService
    {
        public DataBaseWriterService(ScanChannel scanChannel)
        {
             ScanChannel = scanChannel;
        }

        private ScanChannel ScanChannel { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in ScanChannel.MessageChannel.Reader.ReadAllAsync(stoppingToken))
            {
                //Save to DB

                Console.WriteLine($"FROM DBWRITER SERVICE: Message TxID: {message.TransactionID}, Message BlockIndex: {message.BlockIndex}");
            }

            Console.WriteLine("DBWRITER SERVICE LOOP EXITED.");
        }
    }
}
