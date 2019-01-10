using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Xabe.Test
{
    public class AcquireBeforeReleased
    {
        [Theory]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        [InlineData(200)]
        public async void TryToAcquireLockBeforeItIsReleased(int lockMilliseconds)
        {
            var timeout = lockMilliseconds - 10;
            var file = new FileInfo(Path.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, lockMilliseconds);
            var secondFileLock =
                await new FileLockWithTimeout(file).TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds),
                    timeout);
            Assert.False(secondFileLock);
            Assert.True(await firstAcquireTask);
        }
    }

    public class AcquireAfterReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        [InlineData(200)]
        public async void TryToAcquireLockAfterItIsReleased(int lockMilliseconds)
        {
            var timeout = lockMilliseconds * 10;
            var file = new FileInfo(Path.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, lockMilliseconds);
            var secondFileLock = await new FileLockWithTimeout(file).TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
            Assert.True(secondFileLock);
            Assert.True(await firstAcquireTask);
        }
    }

    public class AcquireJustWhenReleased {
        [Theory]
        [InlineData(15)]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        [InlineData(200)]
        public async void TryAcquireLockJustWhenReleased(int lockMilliseconds)
        {
            var timeout = lockMilliseconds;
            var file = new FileInfo(Path.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, lockMilliseconds);
            var secondFileLock = await new FileLockWithTimeout(file).TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
            Assert.True(secondFileLock);
            Assert.True(await firstAcquireTask);
        }
    }

    public class Helpers
    {
        public static async Task<bool> AcquireLockAndReleaseAfterDelay(FileInfo file, int lockMilliseconds)
        {
            ILockWithTimeout fileLock = new FileLockWithTimeout(file);
            if (!fileLock.TryAcquire(TimeSpan.FromMilliseconds(lockMilliseconds)).Result)
            {
                return false;
            }

            using (fileLock)
            {
                await Task.Delay(lockMilliseconds);
                return true;
            }
        }
    }
}
