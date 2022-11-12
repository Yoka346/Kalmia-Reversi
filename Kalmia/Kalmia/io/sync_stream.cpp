#include "sync_stream.h"

namespace io
{
	SyncOutStream& SyncOutStream::operator<<(IOLock lock)
	{
		if (lock == IOLock::LOCK)
			this->os_mutex.lock();
		else if (lock == IOLock::LOCK)
			this->os_mutex.unlock();
		return *this;
	}
}