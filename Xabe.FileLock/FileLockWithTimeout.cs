﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Xabe
{
    /// <inheritdoc />
    /// <summary>
    ///     Providing file locks
    /// </summary>
    public class FileLockWithTimeout : ILockWithTimeout
    {
        /// <summary>
        ///     Minimum allowed milliseconds to timeout or retry
        /// </summary>
        public const int MinimumMilliseconds = 15;

        private readonly TimeSpan _defaultRetryTime = TimeSpan.FromMilliseconds(MinimumMilliseconds);
        private const string Extension = "lock";
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly TimeoutLockModel _content;
        private readonly string _path;

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
            _cancellationTokenSource.Dispose();
            Task.Run(ReleaseLock);
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
        public async Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, TimeSpan timeoutTime)
        {
            return await TryAcquireOrTimeout(lockTime, timeoutTime, timeoutTime);
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, TimeSpan timeoutTime, TimeSpan retryTime)
        {
            if (timeoutTime.TotalMilliseconds < MinimumMilliseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutTime));
            }

            if (retryTime.TotalMilliseconds < MinimumMilliseconds || retryTime > timeoutTime)
            {
                throw new ArgumentOutOfRangeException(nameof(retryTime));
            }
            
            if (!File.Exists(_path))
            {
                return await TryAcquire(lockTime);
            }

            var utcNow = DateTime.UtcNow;
            var utcTimeWithTimeout = utcNow.AddMilliseconds(timeoutTime.TotalMilliseconds);
            var releaseDate = await _content.GetReleaseDate();
            if (releaseDate > utcTimeWithTimeout)
            {
                return false;
            }

            var fallbackPolicy = Policy<bool>
                .Handle<Exception>()
                .FallbackAsync(false);

            var timeToRelease = releaseDate - utcNow;
            //A zero time span is not allowed, hence we put the minimum accepted time of 0.5 milliseconds
            timeToRelease = timeToRelease > TimeSpan.Zero ? timeToRelease : TimeSpan.FromMilliseconds(0.5);
            var releaseTimeoutPolicy = Policy
                .TimeoutAsync<bool>(timeToRelease);
                
            var timeoutPolicy = Policy
                .TimeoutAsync<bool>(timeoutTime);

            var retryPolicy = Policy<bool>
                .HandleResult(r => r == false)
                .WaitAndRetryForeverAsync(retryAttempt => retryTime);

            var defaultRetryPolicy = Policy<bool>
                .HandleResult(r => r == false)
                .WaitAndRetryForeverAsync(retryAttempt => _defaultRetryTime);
            
            var retryBeforeRelease = timeoutTime != retryTime;
            var wrappedPolicy = retryBeforeRelease
                ? Policy.WrapAsync(fallbackPolicy, timeoutPolicy, retryPolicy)
                : Policy.WrapAsync(fallbackPolicy, timeoutPolicy, defaultRetryPolicy, releaseTimeoutPolicy);

            return await wrappedPolicy.ExecuteAsync(async ct => await TryAcquire(lockTime), _cancellationTokenSource.Token);
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

        /// <summary>
        /// Exception is ignored as there is nothing to do if we cannot delete the lock file
        /// </summary>
        private async Task ReleaseLock()
        {
            if (!await IsLockStillValid())
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