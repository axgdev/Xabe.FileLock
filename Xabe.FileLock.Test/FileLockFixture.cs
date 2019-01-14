using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xabe.Test;
using Xunit;
using Xunit.Abstractions;

namespace Xabe.Test
{
    class FileLockFixture: IDisposable
    {
        private const int TimeoutMilliseconds = 1_000;
        private const int PollMilliseconds = 100;

        public void Dispose()
        {
            Directory.Delete(FileLockTestPath.TempFolderPath, true);
            Assert.False(Directory.Exists(FileLockTestPath.TempFolderPath));
        }

        public async Task<bool> TryToDeleteTestFolder()
        {
            if (!Directory.Exists(FileLockTestPath.TempFolderPath))
            {
                return true;
            }

            var timeToDeleteFolder = 0;

            using (var cancellationTokenSource = new CancellationTokenSource(TimeoutMilliseconds))
            {         
                while (Directory.Exists(FileLockTestPath.TempFolderPath))
                {
                    try
                    {
                        Directory.Delete(FileLockTestPath.TempFolderPath, true);
                        Debug.WriteLine($"Time taken to delete test folder: {timeToDeleteFolder} ms");
                        return true;
                    }
                    catch
                    {
                        timeToDeleteFolder += PollMilliseconds;
                        await Task.Delay(PollMilliseconds, cancellationTokenSource.Token);
                    }
                }
            }
            Debug.WriteLine($"Time taken to delete test folder: {timeToDeleteFolder} ms");
            return false;
        }
    }

    [CollectionDefinition(nameof(FileLockCollection))]
    public class FileLockCollection : ICollectionFixture<FileLockFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
