#include "position_eval.h"

#include <iostream>
#include <sstream>
#include <fstream>
#include <random>
#include <type_traits>

#include "../utils/bitmanip.h"

using namespace std;
using namespace reversi;

namespace evaluation
{
	void ValueFuncParam::pack(PackedValueFuncParam& packed_param)
	{
		ValueFuncParamArray* expanded = nullptr;
		PackedValueFuncParamArray* packed = nullptr;
		value_func_param_as_array(*this, expanded);
		packed_value_func_param_as_array(packed_param, packed);
		for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
		{
			auto offset = PATTERN_FEATURE_OFFSET[kind];
			int32_t i = 0;
			for (int32_t f = 0; f < PATTERN_FEATURE_NUM[kind]; f++)
			{
				auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
				if (f <= symmetric_f)
					(*packed)[i++] = (*expanded)[offset + f];
			}
		}
		packed_param.bias = this->bias;
	}

	void PackedValueFuncParam::expand(ValueFuncParam& param)
	{
		PackedValueFuncParamArray* packed = nullptr;
		ValueFuncParamArray* expanded = nullptr;
		packed_value_func_param_as_array(*this, packed);
		value_func_param_as_array(param, expanded);

		int32_t i = 0;
		for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
		{
			auto offset = PATTERN_FEATURE_OFFSET[kind];
			for (int32_t f = 0; f < PATTERN_FEATURE_NUM[kind]; f++)
			{
				auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
				if (symmetric_f < f)
					(*expanded)[offset + f] = (*expanded)[offset + symmetric_f];
				else
					(*expanded)[offset + f] = (*packed)[i++];
			}
		}
		param.bias = this->bias;
	}

	template<ValueRepresentation VALUE_REPS>
	ValueFunction<VALUE_REPS>::ValueFunction(int32_t move_count_per_phase) : empty_count_to_phase(), weight(0)
	{
		this->_phase_num = MAX_EMPTY_COUNT / move_count_per_phase + (MAX_EMPTY_COUNT % move_count_per_phase != 0);
		this->_move_count_per_phase = move_count_per_phase;
		init_empty_count_to_phase_table();

		this->weight = Weight(this->_phase_num);
		for (int32_t i = 0; i < this->weight.length(); i++)
		{
			auto& w = this->weight[i];
			for (auto player = 0; player < 2; player++)
			{
				ValueFuncParamArray* w_array = nullptr;
				value_func_param_as_array(w[player], w_array);
				memset(w_array->as_raw_array(), 0, sizeof(float) * w_array->length());
			}
		}
	}

	// ToDo: reinterpret_castÇópÇ¢ÇƒÇ¢ÇÈïîï™ÇunionÇ≈èëÇ´íºÇ∑.
	template<ValueRepresentation VALUE_REPS>
	ValueFunction<VALUE_REPS>::ValueFunction(const string path) : weight(0)
	{
		ifstream ifs(path, ios_base::in | ios_base::binary);
		if (!ifs)
		{
			ostringstream ss;
			ss << "Cannot open \"" << path << "\".";
			throw invalid_argument(ss.str());
		}

		char b;
		ifs.read(&b, 1);
		bool file_is_little_endian = b;
		ifs.read(&b, 1);
		this->_move_count_per_phase = b;

		this->_phase_num = MAX_EMPTY_COUNT / this->_move_count_per_phase + (MAX_EMPTY_COUNT % this->_move_count_per_phase != 0);
		init_empty_count_to_phase_table();
		this->weight = Weight(this->_phase_num);

		PackedWeight packed_weight(this->_phase_num);
		int32_t phase_count = -1;
		while (!ifs.eof() && ++phase_count != this->_phase_num)
		{
			bool native_is_little_endian = (std::endian::native == std::endian::little);
			if (file_is_little_endian && native_is_little_endian)
				ifs.read(reinterpret_cast<char*>(&packed_weight[phase_count]), sizeof(PackedValueFuncParam));
			else
			{
				PackedValueFuncParamArray* pw_array = nullptr;
				packed_value_func_param_as_array(packed_weight[phase_count], pw_array);
				for (int32_t i = 0; i < pw_array->length(); i++)
				{
					ifs.read(reinterpret_cast<char*>(&(*pw_array)[i]), sizeof(float));
					auto swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&(*pw_array)[i]));
					(*pw_array)[i] = *reinterpret_cast<float*>(&swapped);
				}
			}
		}
		expand_packed_weight(packed_weight);
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::init_empty_count_to_phase_table()
	{
		for (int32_t empty_count = 1; empty_count < this->empty_count_to_phase.length(); empty_count++)
			this->empty_count_to_phase[empty_count] = (MAX_EMPTY_COUNT - empty_count) / this->_move_count_per_phase;

		this->empty_count_to_phase[0] = this->_phase_num - 1;
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::expand_packed_weight(PackedWeight& packed_weight)
	{
		for (size_t i = 0; i < packed_weight.length(); i++)
			packed_weight[i].expand(this->weight[i][reversi::Player::FIRST]);
		copy_player_weight_to_opponent();
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::init_weight_with_rand_num(float mean, float variance)
	{
		std::random_device seed;
		std::default_random_engine rand_eng(seed());
		std::normal_distribution<float> dist(mean, std::sqrtf(variance));

		for (int32_t phase = 0; phase < this->weight.length(); phase++)
		{
			auto& w = this->weight[phase];
			for (auto player = 0; player < 2; player++)
			{
				ValueFuncParamArray* w_array = nullptr;
				value_func_param_as_array(w[player], w_array);
				for (size_t i = 0; i < w_array->length() - 1; i++)
					(*w_array)[i] = dist(rand_eng);
				w[player].bias = 0.0f;
			}
		}
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::save_to_file(const std::string& path)
	{
		ofstream ofs(path, std::ios_base::out | std::ios_base::binary);
		if (!ofs)
		{
			std::ostringstream ss;
			ss << "Cannot open \"" << path << "\".";
			throw std::invalid_argument(ss.str());
		}

		PackedWeight packed_weight(this->_phase_num);
		for (int32_t phase = 0; phase < this->_phase_num; phase++)
		{
			this->weight[phase][Player::FIRST].pack(packed_weight[phase]);
			ofs.write(reinterpret_cast<char*>(&packed_weight[phase]), sizeof(PackedValueFuncParam));
		}
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::copy_player_weight_to_symmetric_pattern_feature()
	{
		for (int32_t i = 0; i < this->_phase_num; i++)
		{
			auto& w = this->weight[i][reversi::Player::FIRST];
			ValueFuncParamArray* param = nullptr;
			value_func_param_as_array(w, param);
			for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
			{
				auto offset = PATTERN_FEATURE_OFFSET[kind];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[kind]; f++)
				{
					auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
					if (f < symmetric_f)
						(*param)[offset + symmetric_f] = (*param)[offset + f];
				}
			}
		}
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::copy_player_weight_to_opponent()
	{
		for (int32_t i = 0; i < this->_phase_num; i++)
		{
			auto& w = this->weight[i];
			ValueFuncParamArray* player_param, * opponent_param;
			player_param = opponent_param = nullptr;
			value_func_param_as_array(w[reversi::Player::FIRST], player_param);
			value_func_param_as_array(w[reversi::Player::SECOND], opponent_param);
			for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
			{
				auto offset = PATTERN_FEATURE_OFFSET[kind];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[kind]; f++)
					(*opponent_param)[offset + TO_OPPONENT_FEATURE[offset + f]] = (*player_param)[offset + f];
			}
			w[reversi::Player::SECOND].bias = w[reversi::Player::FIRST].bias;
		}
	}
}