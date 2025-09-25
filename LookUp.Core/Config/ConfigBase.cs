using System;
using System.Text;

namespace LookUp.Core.Config
{
    public abstract class ConfigBase
    {
        protected ConfigBase(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException($"{nameof(filePath)} cannot be null");
        }

        private readonly object _fileLock = new();

        public string FilePath { get; }

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
