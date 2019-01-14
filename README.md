# Xabe.FileLock.WithTimeout

.NET Standard library providing exclusive lock on file. Additional functionality to acquire this lock with a timeout. Forked from: [Xabe.Filelock](https://github.com/tomaszzmuda/Xabe.FileLock)

## Using ##

Install the [Xabe.FileLock.WithTimeout NuGet package](https://www.nuget.org/packages/Xabe.FileLock.WithTimeout "") via nuget:

	PM> Install-Package Xabe.FileLock.WithTimeout
	
Creating file lock:

	ILock fileLock = new FileLock(file);
	fileLock.TryAcquire(TimeSpan.FromSeconds(15), true);
	
This will create lock file with extension ".lock" in the same directory. Example: "/tmp/data.txt" -> "/tmp/data.lock".

Last parameter is optional and defines if lock should be automatically refreshing before expired.

If file already has lock file, and it time haven't expired, method returns false.

## Recommended using ##

	ILock fileLock = new FileLock(file);
	if(await fileLock.TryAcquire(TimeSpan.FromSeconds(15), true))
	{
		using(fileLock)
		{
			// file operations here
		}
	}
	
## Timeout functionality

Similarly to the code above we can await the FileLock until timeout. Note that refreshing the lock could complicate things:

    ILockWithTimeout fileLock = new FileLockWithTimeout(file);
    if (await fileLock.TryAcquireOrTimeout(TimeSpan.FromSeconds(15)))
    {
        using(fileLock)
        {
            // file operations here
        }
    }
    else
    {
        // things to do if timeout happens
    }
	
## License ## 

Xabe.FileLock is licensed under MIT - see [License](LICENSE.md) for details.
