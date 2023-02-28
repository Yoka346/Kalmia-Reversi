#include "position_eval.h"

#include <iostream>
#include <sstream>
#include <type_traits>

using namespace std;
using namespace reversi;

namespace evaluation
{
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
		ifstream ifs(path, ios_base::binary);
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
				pw.bias = *reinterpret_cast<float*>(&swapped);
			}
		}
		expand_packed_weight(packed_weight);
	}

	template<ValueRepresentation VALUE_REPS>
	void ValueFunction<VALUE_REPS>::expand_packed_weight(PackedWeight& packed_weight)
	{
		ValueFuncParam<float> param;
		for (size_t i = 0; i < packed_weight.length(); i++)
		{
			packed_weight[i].expand(param);
			// ロードした重みは, WEIGHT_SCALEでスケーリングして整数に変換する. メモリ使用量の節約と高速化のため.
			param.scale<int16_t>(this->weight[i][reversi::Player::FIRST], WEIGHT_SCALE);
		}
		copy_player_weight_to_opponent();
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
		ValueFuncParam<float> param;	
		for (int32_t phase = 0; phase < this->_phase_num; phase++)
		{
			auto& pw = packed_weight[phase];
			// ファイルに保存するときは, 1 / WEIGHT_SCALE 倍してスケールを戻し, float型として保存する.
			// 改修の過程でWIGHT_SCALEの値が変わっても, 過去に学習した重みを使えるようにするため.
			this->weight[phase][Player::FIRST].scale<float>(param, 1.0f / WEIGHT_SCALE);
			param.pack(pw);
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