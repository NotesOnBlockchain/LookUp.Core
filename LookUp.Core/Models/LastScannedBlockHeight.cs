namespace LookUp.Core.Models
{
    public class LastScannedBlockHeight
    {
        public LastScannedBlockHeight(int lastScannedBlockHeight, string lastScannedBlockHeightFilePath)
        {
            BlockHeight = lastScannedBlockHeight;
            LastScannedBlockHeightFilePath = lastScannedBlockHeightFilePath;
        }

        public int BlockHeight { get; set; } = 0;
        private object LastScannedBlockHeightLock { get; set; } = new object();
        private string LastScannedBlockHeightFilePath { get; }


        public void IncreaseLastScannedBlockHeight(int processedBlockCount)
        {
            lock (LastScannedBlockHeightLock)
            {
                BlockHeight += processedBlockCount;
            }
        }

        public void SaveLastScannedBlockHeight()
        {
            lock (LastScannedBlockHeightLock)
            {
                File.WriteAllText(LastScannedBlockHeightFilePath, BlockHeight.ToString());
            }
        }

        public static LastScannedBlockHeight LoadFromFile(string filePath)
        {
            try
            {
                using var lastScannedBlockFile = File.OpenRead(filePath);
                var decoder = Serialization.JsonDecoder.FromStream(Serialization.Decode.Int64);
                var lastScannedBlockHeightResult = decoder(lastScannedBlockFile);

                long lastScannedBlockHeight = lastScannedBlockHeightResult.Match(value => value, error => throw new InvalidOperationException(error));

                return new LastScannedBlockHeight((int)lastScannedBlockHeight, filePath);
            }
            catch (Exception)
            {
                File.WriteAllText(filePath, "0");
                return new LastScannedBlockHeight(0, filePath);
            }
        }
    }
}
