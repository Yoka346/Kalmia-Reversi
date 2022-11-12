#pragma once
#include <stdint.h>

#include "../../utils/array.h"

namespace search::endgame
{
	struct TTEntry
	{
		int8_t upper_bound;
		int8_t lower_bound;
		uint64_t hash_code;
		bool is_used;
	};

	class TranspositionTable
	{
	public:
		TranspositionTable(size_t max_size) : entries(calc_table_length(max_size)) { ; }
		void clear();

		TTEntry* get_entry(uint64_t hash_code)
		{
			size_t idx = hash_code & (this->entries.length() - 1);	// ƒGƒ“ƒgƒŠ‚Ì”‚Í2^n‚È‚Ì‚Å, ‚±‚ÌŽ®‚ÌˆÓ–¡‚Í hash_code % this->entries.length()‚Æ“¯‚¶ˆÓ–¡.
			if (this->entries[idx].is_used && this->entries[idx].hash_code == hash_code)
				return &this->entries[idx];
			return nullptr;
		}

		void set_entry(uint64_t hash_code, int8_t lower_bound, int8_t upper_bound)
		{
			size_t idx = hash_code & (this->entries.length() - 1);
			this->entries[idx].lower_bound = lower_bound;
			this->entries[idx].upper_bound = upper_bound;
			this->entries[idx].is_used = true;
		}

	private:
		utils::DynamicArray<TTEntry> entries;
		static size_t calc_table_length(size_t max_size);
	};
}