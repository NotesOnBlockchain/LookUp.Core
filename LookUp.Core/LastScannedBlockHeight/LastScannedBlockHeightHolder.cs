using LookUp.Serialization;

namespace LookUp.Scanner.LastScannedBlockHeight
{
    public class LastScannedBlockHeightHolder
    {
        public LastScannedBlockHeightHolder(int lastScannedBlockHeight, string lastScannedBlockHeightFilePath)
        {
            BlockHeight = lastScannedBlockHeight;
            LastScannedBlockHeightFilePath = lastScannedBlockHeightFilePath;
        }

        public int BlockHeight { get; set; } = 0;
        private object LastScannedBlockHeightLock { get; set; } = new object();
        private string LastScannedBlockHeightFilePath { get; }


        public void IncreaseLastScannedBlockHeight(int blockHeight)
        {
            lock (LastScannedBlockHeightLock)
            {
                BlockHeight = blockHeight;
                File.WriteAllText(LastScannedBlockHeightFilePath, BlockHeight.ToString());
            }
        }

        public static LastScannedBlockHeightHolder LoadFromFile(string filePath)
        {
            try
            {
                using var lastScannedBlockFile = File.OpenRead(filePath);
                var decoder = JsonDecoder.FromStream(Decode.Int64);
                var lastScannedBlockHeightResult = decoder(lastScannedBlockFile);

                long lastScannedBlockHeight = lastScannedBlockHeightResult.Match(value => value, error => throw new InvalidOperationException(error));

                return new LastScannedBlockHeightHolder((int)lastScannedBlockHeight, filePath);
            }
            catch (Exception)
            {
                File.WriteAllText(filePath, "0");
                return new LastScannedBlockHeightHolder(0, filePath);
            }
        }
    }
}
