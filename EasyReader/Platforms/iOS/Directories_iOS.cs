using System;
using System.IO;
using EasyReader.Services;

namespace EasyReader.Platforms.iOS
{
    public class Directories_iOS : IDirectoryCreator
    {
        readonly string externalBasePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public string CreateDirectory(string directoryName)
        {
            var directoryPath = Path.Combine(externalBasePath, directoryName);
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }
    }
}
