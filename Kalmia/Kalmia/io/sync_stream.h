#pragma once
#include <iostream>
#include <mutex>

namespace io
{
	enum class IOLock
	{
		LOCK,
		UNLOCK
	};

	class SyncOutStream
	{
	public:
		SyncOutStream(std::ostream* os) : os(os), os_mutex() { ; }

		SyncOutStream& operator<<(IOLock lock);

		template<class CharT, class Traits>
		SyncOutStream& operator<<(std::basic_ostream<CharT, Traits>& os)
		{
			*this->os << os;
			return *this;
		}

		template<class T>
		SyncOutStream& operator<<(T value)
		{
			*this->os << value;
			return *this;
		}

		void flush() 
		{ 
			this->os->flush(); 
			std::unique_lock<std::mutex> lock(this->os_mutex, std::defer_lock);
			if(lock.owns_lock())
				this->os_mutex.unlock(); 
		}

	private:
		std::ostream* os;
		std::mutex os_mutex;
	};
}