using System;
using System.Threading.Tasks;

namespace Xabe
{
    /// <inheritdoc />
    /// <summary>
    ///     Extend functionality by adding timeout to <see cref="ILock"/>
    /// </summary>
    public interface ILockWithTimeout : ILock
    {
        /// <summary>
        /// A method overload that tries once to wait for the release until timeout,
        /// See <see cref="M:TryToAcquireTimeout(TimeSpan, uint, uint)" />
        /// </summary>
        Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, TimeSpan timeoutTime);

        /// <summary>
        ///     Acquire lock with timeout. Maximum resolution around 15ms for Windows (Task.Delay)
        /// </summary>
        /// <param name="lockTime">Amount of time after that lock is released</param>
        /// <param name="timeoutTime">Amount of time until timeout. Minimum: 15ms</param>
        /// <param name="retryTime">Amount of time to wait to retry acquiring the lock before the official release. Minimum: 15ms, Maximum: <paramref name="timeoutTime"/></param>
        /// <returns>File lock. False if lock already exists</returns>
        Task<bool> TryAcquireOrTimeout(TimeSpan lockTime, TimeSpan timeoutTime, TimeSpan retryTime);
    }
}