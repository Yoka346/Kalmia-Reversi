#include "position_eval.h"

#include <iostream>
#include <sstream>
#include <type_traits>

using namespace std;
using namespace reversi;

namespace evaluation
{
	void ValueFuncParam::pack(PackedValueFuncParam& packed_param)
	{
		pack<PatternKind::CORNER3x3>(this->corner3x3, packed_param.corner3x3);
		pack<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, packed_param.corner_edge_x);
		pack<PatternKind::EDGE_2X>(this->edge_2x, packed_param.edge_2x);
		pack<PatternKind::CORNER2x5>(this->corner2x5, packed_param.corner2x5);
		pack<PatternKind::LINE0>(this->line0, packed_param.line0);
		pack<PatternKind::LINE1>(this->line1, packed_param.line1);
		pack<PatternKind::LINE2>(this->line2, packed_param.line2);
		pack<PatternKind::DIAG_LINE8>(this->diag_line8, packed_param.diag_line8);
		pack<PatternKind::DIAG_LINE7>(this->diag_line7, packed_param.diag_line7);
		pack<PatternKind::DIAG_LINE6>(this->diag_line6, packed_param.diag_line6);
		pack<PatternKind::DIAG_LINE5>(this->diag_line5, packed_param.diag_line5);
		pack<PatternKind::DIAG_LINE4>(this->diag_line4, packed_param.diag_line4);
		packed_param.bias = this->bias;
	}

	void ValueFuncParam::to_opponent(ValueFuncParam& out)
	{
		to_opponent<PatternKind::CORNER3x3>(this->corner3x3, out.corner3x3);
		to_opponent<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, out.corner_edge_x);
		to_opponent<PatternKind::EDGE_2X>(this->edge_2x, out.edge_2x);
		to_opponent<PatternKind::CORNER2x5>(this->corner2x5, out.corner2x5);
		to_opponent<PatternKind::LINE0>(this->line0, out.line0);
		to_opponent<PatternKind::LINE1>(this->line1, out.line1);
		to_opponent<PatternKind::LINE2>(this->line2, out.line2);
		to_opponent<PatternKind::DIAG_LINE8>(this->diag_line8, out.diag_line8);
		to_opponent<PatternKind::DIAG_LINE7>(this->diag_line7, out.diag_line7);
		to_opponent<PatternKind::DIAG_LINE6>(this->diag_line6, out.diag_line6);
		to_opponent<PatternKind::DIAG_LINE5>(this->diag_line5, out.diag_line5);
		to_opponent<PatternKind::DIAG_LINE4>(this->diag_line4, out.diag_line4);
		out.bias = this->bias;
	}

	void ValueFuncParam::clear()
	{
		this->corner3x3.clear();
		this->corner_edge_x.clear();
		this->edge_2x.clear();
		this->corner2x5.clear();
		this->line0.clear();
		this->line1.clear();
		this->line2.clear();
		this->diag_line8.clear();
		this->diag_line7.clear();
		this->diag_line6.clear();
		this->diag_line5.clear();
		this->diag_line4.clear();
		this->bias = 0.0f;
	}

	void ValueFuncParam::init_with_rand(normal_distribution<float>& dist, default_random_engine& eng)
	{
		init_with_rand<PatternKind::CORNER3x3>(this->corner3x3, dist, eng);
		init_with_rand<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, dist, eng);
		init_with_rand<PatternKind::EDGE_2X>(this->edge_2x, dist, eng);
		init_with_rand<PatternKind::CORNER2x5>(this->corner2x5, dist, eng);
		init_with_rand<PatternKind::LINE0>(this->line0, dist, eng);
		init_with_rand<PatternKind::LINE1>(this->line1, dist, eng);
		init_with_rand<PatternKind::LINE2>(this->line2, dist, eng);
		init_with_rand<PatternKind::DIAG_LINE8>(this->diag_line8, dist, eng);
		init_with_rand<PatternKind::DIAG_LINE7>(this->diag_line7, dist, eng);
		init_with_rand<PatternKind::DIAG_LINE6>(this->diag_line6, dist, eng);
		init_with_rand<PatternKind::DIAG_LINE5>(this->diag_line5, dist, eng);
		init_with_rand<PatternKind::DIAG_LINE4>(this->diag_line4, dist, eng);
		this->bias = 0.0f;
	}

	void PackedValueFuncParam::expand(ValueFuncParam& param)
	{
		expand<PatternKind::CORNER3x3>(this->corner3x3, param.corner3x3);
		expand<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, param.corner_edge_x);
		expand<PatternKind::EDGE_2X>(this->edge_2x, param.edge_2x);
		expand<PatternKind::CORNER2x5>(this->corner2x5, param.corner2x5);
		expand<PatternKind::LINE0>(this->line0, param.line0);
		expand<PatternKind::LINE1>(this->line1, param.line1);
		expand<PatternKind::LINE2>(this->line2, param.line2);
		expand<PatternKind::DIAG_LINE8>(this->diag_line8, param.diag_line8);
		expand<PatternKind::DIAG_LINE7>(this->diag_line7, param.diag_line7);
		expand<PatternKind::DIAG_LINE6>(this->diag_line6, param.diag_line6);
		expand<PatternKind::DIAG_LINE5>(this->diag_line5, param.diag_line5);
		expand<PatternKind::DIAG_LINE4>(this->diag_line4, param.diag_line4);
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
			w[DiscColor::BLACK].clear();
			w[DiscColor::WHITE].clear();
		}
	}

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
		endian file_endian = b ? endian::little : endian::big;

		ifs.read(&b, 1);
		this->_move_count_per_phase = b;

		this->_phase_num = MAX_EMPTY_COUNT / this->_move_count_per_phase + (MAX_EMPTY_COUNT % this->_move_count_per_phase != 0);
		init_empty_count_to_phase_table();
		load_weight(ifs, file_endian != endian::native);
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::init_empty_count_to_phase_table()
	{
		for (int32_t empty_count = 1; empty_count < this->empty_count_to_phase.length(); empty_count++)
			this->empty_count_to_phase[empty_count] = (MAX_EMPTY_COUNT - empty_count) / this->_move_count_per_phase;

		this->empty_count_to_phase[0] = this->_phase_num - 1;
	}


	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::load_weight(ifstream& ifs, bool swap_byte)
	{
		this->weight = Weight(this->_phase_num);

		PackedWeight packed_weight(this->_phase_num);
		int32_t phase_count = -1;
		while (!ifs.eof() && ++phase_count != this->_phase_num)
		{
			auto& pw = packed_weight[phase_count];
			read_param<PatternKind::CORNER3x3>(ifs, pw.corner3x3, swap_byte);
			read_param<PatternKind::CORNER_EDGE_X>(ifs, pw.corner_edge_x, swap_byte);
			read_param<PatternKind::EDGE_2X>(ifs, pw.edge_2x, swap_byte);
			read_param<PatternKind::CORNER2x5>(ifs, pw.corner2x5, swap_byte);
			read_param<PatternKind::LINE0>(ifs, pw.line0, swap_byte);
			read_param<PatternKind::LINE1>(ifs, pw.line1, swap_byte);
			read_param<PatternKind::LINE2>(ifs, pw.line2, swap_byte);
			read_param<PatternKind::DIAG_LINE8>(ifs, pw.diag_line8, swap_byte);
			read_param<PatternKind::DIAG_LINE7>(ifs, pw.diag_line7, swap_byte);
			read_param<PatternKind::DIAG_LINE6>(ifs, pw.diag_line6, swap_byte);
			read_param<PatternKind::DIAG_LINE5>(ifs, pw.diag_line5, swap_byte);
			read_param<PatternKind::DIAG_LINE4>(ifs, pw.diag_line4, swap_byte);
			ifs.read(reinterpret_cast<char*>(&pw.bias), sizeof(float));
			if (swap_byte)
			{
				auto swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&pw.bias));
				pw.bias = *reinterpret_cast<float*>(&pw.bias);
			}
		}
		expand_packed_weight(packed_weight);
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
			for (auto player = 0; player < 2; player++)
				this->weight[phase][player].init_with_rand(dist, rand_eng);
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

		char b = static_cast<char>(endian::native == endian::little);
		ofs.write(&b, 1);

		b = static_cast<char>(this->_move_count_per_phase);
		ofs.write(&b, 1);

		PackedWeight packed_weight(this->_phase_num);
		for (int32_t phase = 0; phase < this->_phase_num; phase++)
		{
			auto& pw = packed_weight[phase];
			this->weight[phase][Player::FIRST].pack(pw); 
			write_param<PatternKind::CORNER3x3>(ofs, pw.corner3x3);
			write_param<PatternKind::CORNER_EDGE_X>(ofs, pw.corner_edge_x);
			write_param<PatternKind::EDGE_2X>(ofs, pw.edge_2x);
			write_param<PatternKind::CORNER2x5>(ofs, pw.corner2x5);
			write_param<PatternKind::LINE0>(ofs, pw.line0);
			write_param<PatternKind::LINE1>(ofs, pw.line1);
			write_param<PatternKind::LINE2>(ofs, pw.line2);
			write_param<PatternKind::DIAG_LINE8>(ofs, pw.diag_line8);
			write_param<PatternKind::DIAG_LINE7>(ofs, pw.diag_line7);
			write_param<PatternKind::DIAG_LINE6>(ofs, pw.diag_line6);
			write_param<PatternKind::DIAG_LINE5>(ofs, pw.diag_line5);
			write_param<PatternKind::DIAG_LINE4>(ofs, pw.diag_line4);
			ofs.write(reinterpret_cast<char*>(&pw.bias), sizeof(float));
		}
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::copy_player_weight_to_opponent()
	{
		for (int32_t i = 0; i < this->_phase_num; i++)
		{
			auto& w = this->weight[i];
			auto& player_param = w[Player::FIRST];
			auto& opponent_param = w[Player::SECOND];
			player_param.to_opponent(opponent_param);
		}
	}
}