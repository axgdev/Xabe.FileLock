using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Xabe.Test
{
    [Collection(nameof(FileLockCollection))]
    public class AcquireBeforeReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        public async void TryToAcquireLockBeforeItIsReleased(int lockMilliseconds)
        {
            var timeout = FileLockWithTimeout.MinimumMilliseconds;
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, lockMilliseconds);
            using (var secondLock = new FileLockWithTimeout(file))
            {
                var secondFileLock = await secondLock.TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
                Assert.False(secondFileLock);
            }

            Assert.True(await firstAcquireTask);
        }
    }

    [Collection(nameof(FileLockCollection))]
    public class AcquireAfterReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        public async void TryToAcquireLockAfterItIsReleased(int lockMilliseconds)
        {
            var timeout = lockMilliseconds * 10;
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, 0);
            using (var secondLock = new FileLockWithTimeout(file))
            {
                var secondFileLock = await secondLock.TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
                Assert.True(secondFileLock);
            }

            Assert.True(await firstAcquireTask);
        }
    }

    [Collection(nameof(FileLockCollection))]
    public class AcquireJustWhenReleased
    {
        [Theory]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        public async void TryAcquireLockJustWhenReleased(int lockMilliseconds)
        {
            var timeout = lockMilliseconds;
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = await Helpers.AcquireLockAndReleaseAfterDelay(file, FileLockWithTimeout.MinimumMilliseconds);
            using (var secondLock = new FileLockWithTimeout(file))
            {
                var secondFileLock = await secondLock.TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
                Assert.True(secondFileLock);
            }

            Assert.True(firstAcquireTask);
        }
    }

    [Collection(nameof(FileLockCollection))]
    public class AcquireLockBeforeOfficialRelease
    {
        //Minimum time is 15ms. So the lockMilliseconds (x) should be x/2 > 30ms, because if x/3 >= 15ms
        //there is time to try a second time without timing out. Besides x % 6 == 0, to make it divisible
        //between 3 and 2.
        [Theory]
        [InlineData(66)]
        [InlineData(72)]
        [InlineData(78)]
        [InlineData(84)]
        [InlineData(90)]
        public async void TryToAcquireLockBeforeOfficialRelease(int lockMilliseconds)
        {
            var timeToRelease = lockMilliseconds / 3;
            var maximumTimeToWait = lockMilliseconds / 2;
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, timeToRelease);
            using (var secondLock = new FileLockWithTimeout(file))
            {
                var secondLockTimeout = TimeSpan.FromMilliseconds(lockMilliseconds);
                const int secondLockRetryMs = FileLockWithTimeout.MinimumMilliseconds;
                var timeoutMs = lockMilliseconds;
                var secondFileLock = secondLock.TryAcquireOrTimeout(secondLockTimeout, timeoutMs, secondLockRetryMs);
                Assert.True(await Task.WhenAny(secondFileLock, Task.Delay(maximumTimeToWait)) == secondFileLock);
            }

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
            }

            return true;
        }
    }
}