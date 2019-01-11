using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Path = Xabe.Test.FileLockTestPath;

namespace Xabe.Test
{
    public class FileCleanDisposable: IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Path.TempFolderPath))
            {
                Directory.Delete(Path.TempFolderPath, true);
            }
            Assert.False(Directory.Exists(Path.TempFolderPath));
        }
    }
}
