﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Xabe
{
    /// <summary>
    ///     Providing file locks
    /// </summary>
    public class FileLock : ILock
    {
        private const string Extension = "lock";
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly LockModel _content;
        private readonly string _path;

        private FileLock()
        {
        }

        /// <inheritdoc />
        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="fileToLock">File we want lock</param>
        public FileLock(FileInfo fileToLock) : this(fileToLock.FullName)
        {
        }

        /// <summary>
        ///     Creates reference to file lock on target file
        /// </summary>
        /// <param name="path">Path to file we want lock</param>
        public FileLock(string path)
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
            if(!File.Exists(_path))
            {
                return await _content.TrySetReleaseDate(DateTime.UtcNow + lockTime);
            }

            if (File.Exists(_path) && await _content.GetReleaseDate() > DateTime.UtcNow)
            {
                return false;
            }

            if(!await _content.TrySetReleaseDate(DateTime.UtcNow + lockTime))
            {
                return false;
            }

            if(refreshContinuously)
            {
                ContinuousRefreshTask(lockTime);
            }

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> TryAcquireWithTimeout(TimeSpan lockTime, uint timeoutMilliseconds, uint retryMilliseconds = 0)
        {
            if (retryMilliseconds < 15 || retryMilliseconds >= timeoutMilliseconds)
                throw new Exception($"Retry Milliseconds ({retryMilliseconds}ms) are lower than 15ms or higher than timeout ({timeoutMilliseconds} ms)");

            if (!File.Exists(_path))
            {
                return await TryAcquire(lockTime);
            }

            var utcTimeNow = DateTime.UtcNow;
            var utcTimeWithTimeout = utcTimeNow.AddMilliseconds(timeoutMilliseconds);
            var releaseDate = await _content.GetReleaseDate();
            if (releaseDate > utcTimeWithTimeout)
            {
                return false;
            }

            var millisecondsToWait = (releaseDate - utcTimeNow).Milliseconds;
            await Task.Delay(millisecondsToWait > 0 ? millisecondsToWait : 0);
            return await TryAcquire(lockTime);
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
