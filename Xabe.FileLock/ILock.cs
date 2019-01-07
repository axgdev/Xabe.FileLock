using System;
using System.Threading.Tasks;

namespace Xabe
{
    /// <summary>
    ///     Implementation of FileLock
    /// </summary>
    public interface ILock : IDisposable
    {
        /// <summary>
        ///     Extend lock by certain amount of time
        /// </summary>
        /// <param name="lockTime">How much time add to lock</param>
        Task AddTime(TimeSpan lockTime);

        /// <summary>
        ///     Get current lock release date
        /// </summary>
        /// <returns>Estimated date when lock gets released. DateTime.MaxValue if no lock exists.</returns>
        Task<DateTime> GetReleaseDate();

        /// <summary>
        ///     Acquire lock.
        /// </summary>
        /// <param name="releaseDate">Date after that lock is released</param>
        /// <returns>File lock. False if lock already exists.</returns>
        Task<bool> TryAcquire(DateTime releaseDate);

        /// <summary>
        ///     Acquire lock.
        /// </summary>
        /// <param name="lockTime">Amount of time after that lock is released</param>
        /// <param name="refreshContinuously">Specify if FileLock should automatically refresh lock.</param>
        /// <returns>File lock. False if lock already exists.</returns>
        Task<bool> TryAcquire(TimeSpan lockTime, bool refreshContinuously = false);

        /// <summary>
        ///     Acquire lock with timeout. Maximum resolution around 15ms for Windows (Task.Delay)
        /// </summary>
        /// <param name="lockTime">Amount of time after that lock is released</param>
        /// <param name="timeoutMilliseconds">Amount of milliseconds until timeout</param>
        /// <param name="retryMilliseconds">Amount of milliseconds to wait to retry acquiring the lock. Minimum: 15ms, Maximum: <paramref name="timeoutMilliseconds"/></param>
        /// <returns>File lock. False if lock already exists</returns>
        Task<bool> TryAcquireWithTimeout(TimeSpan lockTime, uint timeoutMilliseconds, uint retryMilliseconds);
    }
}
