using System;
using System.IO;

namespace CoreWCF.ConfigurationManager.Tests
{
    class TemporaryFileStream : IDisposable
    {
        private readonly FileStream _fileStream;
        private readonly string _path;
        public string Name => _fileStream.Name;

        public TemporaryFileStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _path = path;
        }


        public static TemporaryFileStream Create(string content)
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, content);

            return new TemporaryFileStream(path);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            _fileStream.Dispose();
            if (disposing)
            {
                File.Delete(_path);
            }
        }
    }
}
