using System;
using System.Threading.Tasks;

namespace Xabe
{
    internal class TimeoutLockModel : LockModel
    {
        internal DateTime CachedReleaseDate;

        public TimeoutLockModel(string path) : base(path)
        {
        }

        internal new async Task<bool> TrySetReleaseDate(DateTime date)
        {
            var releaseDateIsSet = await base.TrySetReleaseDate(date);
            if (releaseDateIsSet)
            {
                CachedReleaseDate = date;
            }

            return releaseDateIsSet;
        }
    }
}