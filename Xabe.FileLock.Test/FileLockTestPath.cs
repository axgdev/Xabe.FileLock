using System.IO;

namespace Xabe.Test
{
    internal class FileLockTestPath
    {
        public static readonly string TempFolderPath = Path.Combine(Path.GetTempPath(), "FileLockTests");

        public static string GetTempFileName()
        {
            if (!Directory.Exists(TempFolderPath))
            {
                Directory.CreateDirectory(TempFolderPath);
            }
            var fileInfo = new FileInfo(Path.Combine(TempFolderPath, Path.GetRandomFileName()));
            fileInfo.Create();
            return fileInfo.Name;
        }

        public static string ChangeExtension(string path, string extension)
        {
            return Path.ChangeExtension(path, extension);
        }
    }
}