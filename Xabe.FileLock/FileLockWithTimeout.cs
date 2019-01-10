using System;
using System.Collections.Generic;
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
        private const int MinimumMilliseconds = 50;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly TimeoutLockModel _content;
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
            _content = new TimeoutLockModel(_path);
        }

        /// <inheritdoc />
        /// <summary>
        ///     Stop refreshing lock and delete lock when it makes sense. IOException is ignored for cases when file in use
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            ReleaseLock();
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

            if (await _content.GetReleaseDate() > DateTime.UtcNow)
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

            return await RunAcquireUntilTimeout(lockTime, timeoutMilliseconds, retryMilliseconds, releaseDate);
        }

        private async Task<bool> RunAcquireUntilTimeout(TimeSpan lockTime, int timeoutMilliseconds, int retryMilliseconds, DateTime releaseDate)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Task<bool>[] tasksToExecute;
                var timeoutTask = TimeoutTask(timeoutMilliseconds, cancellationTokenSource.Token);
                if (retryMilliseconds == timeoutMilliseconds)
                {
                    var waitTillReleaseAcquire = WaitTillReleaseAcquire(lockTime, releaseDate, cancellationTokenSource.Token);
                    tasksToExecute = new[] { waitTillReleaseAcquire, timeoutTask };
                }
                else
                {
                    var retryTask = RetryBeforeRelease(lockTime, releaseDate, retryMilliseconds, cancellationTokenSource.Token);
                    tasksToExecute = new[] { retryTask, timeoutTask };
                }

                var completedTask = await Task.WhenAny(tasksToExecute);
                if (completedTask.IsCanceled)
                {
                    throw new Exception("Task should have not been canceled");
                }

                cancellationTokenSource.Cancel();
                return await completedTask; // Very important in order to propagate exceptions
            }
        }

        private async Task<bool> TimeoutTask(int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            await Task.Delay(timeoutMilliseconds, cancellationToken);
            return false;
        }

        private async Task<bool> RetryBeforeRelease(TimeSpan lockTime, DateTime releaseDate, int retryMilliseconds,
            CancellationToken cancellationToken)
        {
            var timeoutRetryBeforeRelease = releaseDate - DateTime.UtcNow;
            using (var retryCancellationTokenSource = new CancellationTokenSource(timeoutRetryBeforeRelease))
            {
                if (await RetryAcquireLock(lockTime, retryMilliseconds, retryCancellationTokenSource.Token))
                {
                    return true;
                }
            }
            return await RetryAcquireLock(lockTime, MinimumMilliseconds, cancellationToken);
        }

        private async Task<bool> WaitTillReleaseAcquire(TimeSpan lockTime, DateTime releaseDate, CancellationToken cancellationToken)
        {
            var millisecondsToWait = (int) Math.Ceiling((releaseDate - DateTime.UtcNow).TotalMilliseconds);
            await Task.Delay(millisecondsToWait > 0 ? millisecondsToWait : 0, cancellationToken);
            return await RetryAcquireLock(lockTime, MinimumMilliseconds, cancellationToken);
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

        private void ReleaseLock()
        {
            if (!IsLockStillValid().Result)
            {
                return;
            }

            try
            {
                File.Delete(_path);
            }
            catch (IOException)
            {
            }
        }

        private async Task<bool> IsLockStillValid()
        {
            return _content.CachedReleaseDate != DateTime.MinValue &&
                   File.Exists(_path) &&
                   _content.CachedReleaseDate == await GetReleaseDate();
        }

        private string GetLockFileName(string path)
        {
            return Path.ChangeExtension(path, Extension);
        }
    }
}