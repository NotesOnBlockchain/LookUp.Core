using System.Text;

namespace LookUp.Config
{
    public abstract class ConfigBase
    {
        protected ConfigBase(string filePath, string apiKey)
        {
            FilePath = filePath ?? throw new ArgumentNullException($"{nameof(filePath)} cannot be null");
            APIKey = apiKey ?? throw new ArgumentNullException($"{nameof(apiKey)} cannot be null");
        }

        private readonly object _fileLock = new();

        public string FilePath { get; }

        public string APIKey { get; }

        public void ToFile()
        {
            lock (_fileLock)
            {
                File.WriteAllText(FilePath, EncodeAsJson(), Encoding.UTF8);
            }
        }

        protected abstract string EncodeAsJson();
    }
}
