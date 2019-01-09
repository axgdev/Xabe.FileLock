using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Xabe
{
    internal class TimeoutLockModel: LockModel
    {
        public TimeoutLockModel(string path) : base(path)
        {
        }

        internal DateTime CachedReleaseDate;

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
