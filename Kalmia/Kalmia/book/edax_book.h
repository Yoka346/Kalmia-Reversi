#pragma once
#include <string>
#include <fstream>
#include <stdexcept>

#include "../utils/array.h"
#include "../reversi/position.h"

namespace book
{
	struct EdaxBookLink
	{
		int8_t score;
		reversi::BoardCoordinate move;

		EdaxBookLink() : score(0), move(reversi::BoardCoordinate::NULL_COORD) { ; }
	};

	struct EdaxBookPosition
	{
		reversi::Bitboard board;
		EdaxBookLink leaf;
		utils::DynamicArray<EdaxBookLink> links;
		uint32_t win_count = 0;
		uint32_t draw_count = 0;
		uint32_t loss_count = 0;
		uint32_t unterminated_line_count = 0;
		struct { int16_t value = 0, lower = 0, upper = 0; } score;
		uint8_t level = 0;

		EdaxBookPosition() : board(0ULL, 0ULL), leaf(), links(0) { ; }
	};

	/**
	* @class
	* @brief EdaxŒ`Ž®‚ÌBook
	**/
	class EdaxBook
	{
		using Positions = utils::DynamicArray<EdaxBookPosition>;

	public:
		EdaxBook(const std::string& path);

		const EdaxBookPosition* begin() const { return this->_positions.begin(); }
		const EdaxBookPosition* end() const { return this->_positions.end(); }
		const EdaxBookPosition& operator[](size_t idx) { return this->_positions[idx]; }

	private:
		static constexpr size_t LABEL_SIZE = 4;
		static constexpr size_t DATA_KIND_SIZE = 4;
		static constexpr const char* BIG_ENDIAN_LABEL = "EDAX";
		static constexpr const char* LITTLE_ENDIAN_LABEL = "XADE";
		static constexpr const char* BIG_ENDIAN_DATA_KIND = "BOOK";
		static constexpr const char* LITTLE_ENDIAN_DATA_KIND = "KOOB";

		std::endian file_endian;
		uint8_t _version;
		uint8_t _release_num;

		struct
		{
			int16_t year;
			int8_t month, day, hour, minute, second;
		} _date;

		struct 
		{
			int32_t level, empty_num, midgame_error, endgame_error, verbosity;
		} _options;

		struct { int32_t node_count, link_count; } _stats;

		Positions _positions;

		bool check_endian(std::ifstream& ifs);
		bool check_if_book(std::ifstream& ifs);
		void load_header(std::ifstream& ifs);
		void load_positions(std::ifstream& ifs);
		void adjust_endian(char* buffer, size_t len);

		template<class T>
		void read_file(std::ifstream& ifs, T& out)
		{
			read_file(ifs, reinterpret_cast<char*>(&out), sizeof(T));
		}

		void read_file(std::ifstream& ifs, char* buffer, size_t count);
	};
}
