using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Xabe.Test
{
    public class FileLockWithTimeoutTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TimeSpan _timeVariable = TimeSpan.FromSeconds(5);
        private const string Extension = "lock";
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public FileLockWithTimeoutTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(15)]
        [InlineData(30)]
        [InlineData(50)]
        [InlineData(70)]
        [InlineData(100)]
        [InlineData(150)]
        [InlineData(200)]
        //[InlineData(5_000)]
        //[InlineData(10_000)]
        public async void Acquire2LockAndOtherProcessWaitForReleaseUntilTimeout(int lockMilliseconds)
        {
            var timeout = 10 * lockMilliseconds;

            var file = new FileInfo(Path.GetTempFileName());
            var firstAcquireTask = AcquireLockAndReleaseAfterDelay(file, lockMilliseconds);
            

            var secondFileLock = await new FileLockWithTimeout(file).TryAcquireOrTimeout(TimeSpan.FromMilliseconds(lockMilliseconds), timeout);
            Assert.True(secondFileLock);
            Assert.True(await firstAcquireTask);
        }

        public async Task<bool> AcquireLockAndReleaseAfterDelay(FileInfo file, int lockMilliseconds)
        {
            ILockWithTimeout fileLock = new FileLockWithTimeout(file);
            if (!await fileLock.TryAcquire(TimeSpan.FromMilliseconds(lockMilliseconds), true))
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
