using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xabe
{
    /// <inheritdoc />
    /// <summary>
    ///     Providing file locks
    /// </summary>
    public class FileLockWithTimeout : ILockWithTimeout
    {
        private const string Extension = "lock";
        private const int MinimumMilliseconds = 15;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly LockModel _content;
        private readonly string _path;

        private FileLockWithTimeout()
        {
        }

        /// <inheritdoc />
        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="fileToLock">File we want lock</param>
        public FileLockWithTimeout(FileInfo fileToLock) : this(fileToLock.FullName)
        {
        }

        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="path">Path to file we want lock</param>
        public FileLockWithTimeout(string path)
        {
            _path = GetLockFileName(path);
            _content = new LockModel(_path);
        }

        /// <inheritdoc />
        /// <summary>
        ///     Stop refreshing lock and delete lock file
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     Extend lock by certain amount of time
        /// </summary>
        /// <param name="lockTime">How much time add to lock</param>
        public async Task AddTime(TimeSpan lockTime)
        {
            await _content.TrySetReleaseDate(await _content.GetReleaseDate() + lockTime);
        }

        /// <inheritdoc />
        /// <summary>
        ///     Get current lock release date
        /// </summary>
        /// <returns>Estimated date when lock gets released. DateTime.MaxValue if no lock exists.</returns>
        public async Task<DateTime> GetReleaseDate()
        {
            return await _content.GetReleaseDate();
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquire(DateTime releaseDate)
        {
            if (File.Exists(_path) && await _content.GetReleaseDate() > DateTime.UtcNow)
            {
                return false;
            }

            return await _content.TrySetReleaseDate(releaseDate);
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquire(TimeSpan lockTime, bool refreshContinuously = false)
        {
            if (!File.Exists(_path))
            {
                return await _content.TrySetReleaseDate(DateTime.UtcNow + lockTime);
            }

            if (File.Exists(_path) && await _content.GetReleaseDate() > DateTime.UtcNow)
            {
                return false;
            }

            if (!await _content.TrySetReleaseDate(DateTime.UtcNow + lockTime))
            {
                return false;
            }

            if (refreshContinuously)
            {
                ContinuousRefreshTask(lockTime);
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, int timeoutMilliseconds)
        {
            return await TryAcquireOrTimeout(lockTime, timeoutMilliseconds, timeoutMilliseconds);
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, int timeoutMilliseconds, int retryMilliseconds)
        {
            if (timeoutMilliseconds < MinimumMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

            if (retryMilliseconds < MinimumMilliseconds || retryMilliseconds > timeoutMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(retryMilliseconds));
            }

            if (!File.Exists(_path))
            {
                return await TryAcquire(lockTime);
            }

            var utcTimeWithTimeout = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            var releaseDate = await _content.GetReleaseDate();
            if (releaseDate > utcTimeWithTimeout)
            {
                return false;
            }

            var waitTillReleaseTryAcquire = WaitTillReleaseTryAcquire(lockTime, releaseDate);

            if (retryMilliseconds == timeoutMilliseconds)
            {
                return await waitTillReleaseTryAcquire;
            }

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var retryTask = RetryAcquireLock(lockTime, retryMilliseconds, cancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(waitTillReleaseTryAcquire, retryTask);
                cancellationTokenSource.Cancel();
                return await completedTask; // Very important in order to propagate exceptions
            }
        }

        private async Task<bool> WaitTillReleaseTryAcquire(TimeSpan lockTime, DateTime releaseDate)
        {
            var millisecondsToWait = (int) Math.Ceiling((releaseDate - DateTime.UtcNow).TotalMilliseconds);
            await Task.Delay(millisecondsToWait > 0 ? millisecondsToWait : 0);
            return await TryAcquire(lockTime);
        }

        private async Task<bool> RetryAcquireLock(TimeSpan lockTime, int retryMilliseconds, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await TryAcquire(lockTime))
                {
                    return true;
                }
                await Task.Delay(retryMilliseconds, cancellationToken);
            }
            return false;
        }

        private void ContinuousRefreshTask(TimeSpan lockTime)
        {
            var refreshTime = (int) (lockTime.TotalMilliseconds * 0.9);
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    await AddTime(TimeSpan.FromMilliseconds(refreshTime));
                    await Task.Delay(refreshTime);
                }
            }, _cancellationTokenSource.Token);
        }

        private string GetLockFileName(string path)
        {
            return Path.ChangeExtension(path, Extension);
        }
    }
}