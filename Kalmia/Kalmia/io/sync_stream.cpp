#include "sync_stream.h"

namespace io
{
	SyncOutStream& SyncOutStream::operator<<(IOLock lock)
	{
		if (lock == IOLock::LOCK)
			this->os_mutex.lock();
		else if (lock == IOLock::LOCK)
			this->os_mutex.unlock();
		return *this;	// mutex‚ğlock‚µ‚Ä‚¢‚é‚Ì‚ÉŠÖ”‚ğ”²‚¯‚é‚Æ‚«‚Éunlock‚µ‚Ä‚¢‚È‚¢‚Æ‚¢‚¤Œx‚ª‚Å‚é‚¯‚Ç, ‚»‚¤‚¢‚¤d—l.
	}
}