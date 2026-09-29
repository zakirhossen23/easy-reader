using System;
using System.IO;
using EasyReader.Services;

namespace EasyReader.Services.PlatformDefaults
{
    public class Directory_Default : IDirectoryCreator
    {
        public string CreateDirectory(string directoryName)
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(basePath, directoryName);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
