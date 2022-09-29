#include "logger.h"

namespace io
{
	template<class T>
	Logger& Logger::operator <<(T t)
	{
		this->ofs << t;
		(*this->sub_os) << t;
		if (this->enabled_auto_flush)
			flush();
		return *this;
	}
}