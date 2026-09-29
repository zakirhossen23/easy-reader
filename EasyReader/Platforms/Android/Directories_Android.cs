using System.IO;
using EasyReader.Services;

namespace EasyReader.Platforms.Android
{
    public class Directories_Android : IDirectoryCreator
    {
        private readonly string externalBasePath = global::Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath;

        public string CreateDirectory(string directoryName)
        {
            string directoryPath = Path.Combine(externalBasePath, directoryName);
            _ = Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }
    }
}
