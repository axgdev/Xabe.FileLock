using System;
using System.Diagnostics;
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
            var timeout = TimeSpan.FromMilliseconds(FileLockWithTimeout.MinimumMilliseconds);
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
            var timeout = TimeSpan.FromMilliseconds(lockMilliseconds * 10);
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = await Helpers.AcquireLockAndReleaseAfterDelay(file, 0);
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
        //Minimum time is 15ms. So the lockMilliseconds (x) should be x > 60ms, because if x/4 >= 15ms there is time
        //to try at least a second time without timing out, because of the timeout in this test set to x - 15ms
        //Besides x % 4 == 0, to make it divisible between 4.
        [Theory]
        [InlineData(64)]
        [InlineData(68)]
        [InlineData(72)]
        [InlineData(76)]
        [InlineData(80)]
        public async void TryToAcquireLockBeforeOfficialRelease(int lockMilliseconds)
        {
            var timeToRelease = lockMilliseconds / 4;
            var maximumTimeToWait = lockMilliseconds - FileLockWithTimeout.MinimumMilliseconds;
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var firstAcquireTask = Helpers.AcquireLockAndReleaseAfterDelay(file, timeToRelease);
            using (var secondLock = new FileLockWithTimeout(file))
            {
                var secondLockTimeout = TimeSpan.FromMilliseconds(lockMilliseconds);
                var secondLockRetryMs = TimeSpan.FromMilliseconds(FileLockWithTimeout.MinimumMilliseconds);
                var timeoutMs = TimeSpan.FromMilliseconds(lockMilliseconds);
                var secondFileLock = secondLock.TryAcquireOrTimeout(secondLockTimeout, timeoutMs, secondLockRetryMs);
                Assert.True(await Task.WhenAny(secondFileLock, Task.Delay(maximumTimeToWait)) == secondFileLock);
            }

            Assert.True(await firstAcquireTask);
        }

    }

    [Collection(nameof(FileLockCollection))]
    public class ShouldGetOutBeforeLockTime
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async void LockShouldNotWaitTillTimeoutToBeAcquiredIfNotLocked(int lockSeconds)
        {
            var file = new FileInfo(FileLockTestPath.GetTempFileName());
            var fileLock = new FileLockWithTimeout(file);
            using (fileLock)
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                await fileLock.TryAcquireOrTimeout(TimeSpan.FromSeconds(lockSeconds),
                    TimeSpan.FromSeconds(lockSeconds));
                stopWatch.Stop();
                Assert.True(stopWatch.ElapsedMilliseconds < lockSeconds * 1000 / 2);
            }
        }
    }

    public class Helpers
    {
        public static async Task<bool> AcquireLockAndReleaseAfterDelay(FileInfo file, int lockMilliseconds)
        {
            ILockWithTimeout fileLock = new FileLockWithTimeout(file);
            if (!await fileLock.TryAcquire(TimeSpan.FromMilliseconds(lockMilliseconds)))
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