#include "tt.h"

#include <cmath>

namespace search::endgame
{
	void TranspositionTable::clear()
	{
		for (TTEntry& entry : this->entries)
			entry.is_used = false;
	}

	size_t TranspositionTable::calc_table_length(size_t max_size)
	{
		if (max_size == 0)
			return 0ULL;
		auto exp = static_cast<int32_t>(std::log2(max_size / sizeof(TTEntry)));
		return 1ULL << exp;
	}
}