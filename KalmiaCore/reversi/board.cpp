#pragma once
#include "board.h"

using namespace reversi;

std::string reversi::coordinate_to_string(BoardCoordinate coord)
{
	auto x = coord % BOARD_SIZE;
	auto y = coord / BOARD_SIZE;
	std::stringstream ss;
	ss << (char)('A' + x) << y + 1;
	return ss.str();
}

inline bool Mobility::move_to_next_coord(BoardCoordinate& coord)
{
	while (this->mobility_count != this->mobility_num)
	{
		if (this->mobility & this->mask)
		{
			this->mobility_count++;
			coord = this->current_coordinate++;
			this->mask <<= 1;
			return true;
		}
		this->current_coordinate++;
		this->mask <<= 1;
	}
	return false;
}

bool Board::initialized = false;
uint64_t Board::hash_rank[Board::HASH_RANK_LEN_0][Board::HASH_RANK_LEN_1];

void Board::static_initializer()
{
	if (Board::initialized)
		return;

	std::random_device seed;
	std::mt19937_64 rand(seed());
	for (auto i = 0; i < 16; i++)
		for (auto j = 0; j < 256; j++)
			hash_rank[i][j] = rand();
	Board::initialized = true;
}

INIT_CALLBACK(Board, Board::static_initializer);

DiscColor Board::get_square_color(BoardCoordinate coord)
{
	auto side_to_move = this->side_to_move + 1;
	auto color = side_to_move * ((this->bitboard.current_player >> coord) & 1) + (side_to_move ^ 3) * ((this->bitboard.opponent_player >> coord) & 1);
	return (color != 0) ? static_cast<DiscColor>(color - 1) : DiscColor::NONE;
}

inline void Board::get_move(BoardCoordinate coord, Move& move)
{
	move.color = this->side_to_move;
	move.coord = coord;
	move.flipped = calc_flipped_discs(this->bitboard.current_player, this->bitboard.opponent_player, coord);
}

inline void Board::get_current_player_mobility(Mobility& mobility)
{
	mobility.set_raw_mobility(calc_mobility(this->bitboard.current_player, this->bitboard.opponent_player));
	mobility.move_to_first();
}

inline void Board::get_opponent_player_mobility(Mobility& mobility)
{
	mobility.set_raw_mobility(calc_mobility(this->bitboard.opponent_player, this->bitboard.current_player));
	mobility.move_to_first();
}

inline void Board::update(Move& move)
{
	auto coord_bit = COORD_TO_BIT[move.coord];
	this->bitboard.opponent_player ^= move.flipped;
	this->bitboard.current_player |= (move.flipped | coord_bit);
	this->side_to_move = opponent_disc_color(this->side_to_move);
	this->bitboard.swap();
}

inline uint64_t Board::get_hash_code()
{
#if defined(USE_AVX2) || defined(USE_SSE41)
	return calc_hash_code_SSE();
#else
	return calc_hash_code_CPU();
#endif
}

inline GameResult Board::get_game_result()
{
	auto diff = this->bitboard.get_current_player_disc_count() - this->bitboard.get_opponent_player_disc_count();
	if (diff > 0)
		return GameResult::WIN;
	if (diff < 0)
		return GameResult::LOSS;
	return GameResult::DRAW;
}

std::string Board::to_string()
{
	std::stringstream ss;
	ss << "  ";
	for (auto i = 0; i < BOARD_SIZE; i++)
		ss << (char)('A' + i) << ' ';

	auto p = this->bitboard.current_player;
	auto o = this->bitboard.opponent_player;
	auto mask = 1ULL;
	for (auto y = 0; y < BOARD_SIZE; y++) 
	{
		ss << '\n' << y + 1 << ' ';
		for (auto x = 0; x < BOARD_SIZE; x++) 
		{
			if (p & mask)
				ss << ((this->side_to_move == DiscColor::BLACK) ? 'X' : 'O') << ' ';
			else if (o & mask)
				ss << ((this->side_to_move != DiscColor::BLACK) ? 'X' : 'O') << ' ';
			else
				ss << ". ";
			mask <<= 1;
		}
	}
	return ss.str();
}

inline uint64_t Board::calc_flipped_discs(uint64_t p, uint64_t o, BoardCoordinate coord)
{
#ifdef USE_AVX2
	return calc_flipped_discs_AVX2(p, o, coord);
#elif defined(USE_SSE41)
	return calc_flipped_discs_SSE(p, o, coord);
#else
	return calc_flipped_discs_CPU(p, o, coord);
#endif 
}

inline uint64_t Board::calc_mobility(uint64_t p, uint64_t o)
{
#ifdef USE_AVX2
	return calc_mobility_AVX2(p, o);
#elif defined(USE_SSE41)
	return calc_mobility_SSE(p, o);
#else
	return calc_mobility_CPU(p, o);
#endif 
}

#ifdef USE_AVX2

/**
 * @fn
 * @brief Calculates flipped discs bit pattern with AVX2.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @param (coord) The coordinate where disc is put.
 * @return Flipped discs bit pattern.
 * @sa calc_flipped_discs_CPU
 * @detail 
 * The flipped discs bit pattern is calculated using parallel prefix algorithm.
 * Flipped discs in 4 directions are calculated in parallel using AVX2.
 * 
 * Term explanation: 
 * Outflank are discs which surround the opponent discs.
 * 
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#movegen (Japanese)
*/
inline uint64_t Board::calc_flipped_discs_AVX2(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	const static __m256i SHIFT = _mm256_set_epi64x(7ULL, 9ULL, 8ULL, 1ULL);
	const static __m256i SHIFT_2 = _mm256_set_epi64x(14ULL, 18ULL, 16ULL, 2ULL);
	const static __m256i MASK = _mm256_set_epi64x(0x7e7e7e7e7e7e7e7eULL, 0x7e7e7e7e7e7e7e7eULL, 0xffffffffffffffffULL, 0x7e7e7e7e7e7e7e7eULL);
	const static __m256i ZERO = _mm256_setzero_si256();

	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(coord_bit));
	auto p_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_4 = _mm256_and_si256(_mm256_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	auto flipped_left_4 = _mm256_and_si256(_mm256_sllv_epi64(coord_bit_4, SHIFT), masked_o_4);
	auto flipped_right_4 = _mm256_and_si256(_mm256_srlv_epi64(coord_bit_4, SHIFT), masked_o_4);

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT), masked_o_4));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT), masked_o_4));

	auto prefix_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));
	auto prefix_right = _mm256_srlv_epi64(prefix_left, SHIFT);

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	auto outflank_left_4 = _mm256_and_si256(p_4, _mm256_sllv_epi64(flipped_left_4, SHIFT));
	auto outflank_right_4 = _mm256_and_si256(p_4, _mm256_srlv_epi64(flipped_right_4, SHIFT));

	flipped_left_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_left_4, ZERO), flipped_left_4);	// ‚±‚±‚Åerror
	flipped_right_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_right_4, ZERO), flipped_right_4);

	auto flipped_4 = _mm256_or_si256(flipped_left_4, flipped_right_4);
	auto flipped_2 = _mm_or_si128(_mm256_extracti128_si256(flipped_4, 0), _mm256_extracti128_si256(flipped_4, 1));
	flipped_2 = _mm_or_si128(flipped_2, _mm_unpackhi_epi64(flipped_2, flipped_2));
	return _mm_cvtsi128_si64(flipped_2);
}

/**
 * @fn
 * @brief Calculates mobility bit pattern with AVX2.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @return Mobility disc pattern.
 * @sa calc_mobility_CPU
 * @detail
 * The mobility bit pattern is calculated using parallel prefix algorithm.
 * The mobilitity in 4 directions are calculated in parallel using AVX2.
 *
 * Term explanation:
 * Mobility is the places where the player can put a disc. 
 *
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility (Japanese)
*/
inline uint64_t Board::calc_mobility_AVX2(uint64_t p, uint64_t o)
{
	const static __m256i SHIFT = _mm256_set_epi64x(7ULL, 9ULL, 8ULL, 1ULL);
	const static __m256i SHIFT_2 = _mm256_set_epi64x(14ULL, 18ULL, 16ULL, 2ULL);
	const static __m256i MASK = _mm256_set_epi64x(0x7e7e7e7e7e7e7e7eULL, 0x7e7e7e7e7e7e7e7eULL, 0xffffffffffffffffULL, 0x7e7e7e7e7e7e7e7eULL);

	auto p_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_4 = _mm256_and_si256(_mm256_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	auto flip_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(p_4, SHIFT));
	auto flip_right = _mm256_and_si256(masked_o_4, _mm256_srlv_epi64(p_4, SHIFT));
	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(flip_left, SHIFT)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(masked_o_4, _mm256_srlv_epi64(flip_right, SHIFT)));

	auto prefix_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));
	auto prefix_right = _mm256_srlv_epi64(prefix_left, SHIFT);

	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(prefix_left, _mm256_sllv_epi64(flip_left, SHIFT_2)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(prefix_right, _mm256_srlv_epi64(flip_right, SHIFT_2)));
	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(prefix_left, _mm256_sllv_epi64(flip_left, SHIFT_2)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(prefix_right, _mm256_srlv_epi64(flip_right, SHIFT_2)));

	auto mobility_4 = _mm256_sllv_epi64(flip_left, SHIFT);
	mobility_4 = _mm256_or_si256(mobility_4, _mm256_srlv_epi64(flip_right, SHIFT));
	auto mobility_2 = _mm_or_si128(_mm256_extractf128_si256(mobility_4, 0), _mm256_extractf128_si256(mobility_4, 1));
	mobility_2 = _mm_or_si128(mobility_2, _mm_unpackhi_epi64(mobility_2, mobility_2));
	return _mm_cvtsi128_si64(mobility_2) & ~(p | o);
}

#elif defined(USE_SSE41) 

/**
 * @fn
 * @brief Calculates flipped discs bit pattern with SSE.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @param (coord) The coordinate where disc is put.
 * @return Flipped discs bit pattern.
 * @sa calc_flipped_discs_CPU
 * @detail
 * The flipped discs bit pattern is calculated using parallel prefix algorithm.
 * The flipped discs in 2 directions are calculated in parallel, in the meantime,
 * The flipped discs in the other 2 directions are calculated using CPU.
 *
 * Term explanation:
 * Outflank are discs which surround the opponent discs.
 * 
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#movegen (Japanese)
*/
inline uint64_t Board::calc_flipped_discs_SSE(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_2 = _mm_set_epi64x(_byteswap_uint64(coord_bit), coord_bit);
	auto p_2 = _mm_set_epi64x(_byteswap_uint64(p), p);
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;
	auto masked_o_2 = _mm_set_epi64x(_byteswap_uint64(masked_o), masked_o);

	// left
	auto flipped_diag_left_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(coord_bit_2, 7));
	auto flipped_horizontal_left = masked_o & (coord_bit << 1);
	auto flipped_vertical_left = o & (coord_bit << 8);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flipped_diag_left_2, 7)));
	flipped_horizontal_left |= masked_o & (flipped_horizontal_left << 1);
	flipped_vertical_left |= o & (flipped_vertical_left << 8);

	auto prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 7));
	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_vertical = o & (o << 8);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_left_2, 14)));
	flipped_horizontal_left |= prefix_horizontal & (flipped_horizontal_left << 2);
	flipped_vertical_left |= prefix_vertical & (flipped_vertical_left << 16);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_left_2, 14)));
	flipped_horizontal_left |= prefix_horizontal & (flipped_horizontal_left << 2);
	flipped_vertical_left |= prefix_vertical & (flipped_vertical_left << 16);

	// right
	auto flipped_diag_right_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(coord_bit_2, 9));
	auto flipped_horizontal_right = masked_o & (coord_bit >> 1);
	auto flipped_vertical_right = o & (coord_bit >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flipped_diag_right_2, 9)));
	flipped_horizontal_right |= masked_o & (flipped_horizontal_right >> 1);
	flipped_vertical_right |= o & (flipped_vertical_right >> 8);

	prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 9));
	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_vertical = o & (o >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_right_2, 18)));
	flipped_horizontal_right |= prefix_horizontal & (flipped_horizontal_right >> 2);
	flipped_vertical_right |= prefix_vertical & (flipped_vertical_right >> 16);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_right_2, 18)));
	flipped_horizontal_right |= prefix_horizontal & (flipped_horizontal_right >> 2);
	flipped_vertical_right |= prefix_vertical & (flipped_vertical_right >> 16);

	auto outflank_diag_left_2 = _mm_and_si128(p_2, _mm_slli_epi64(flipped_diag_left_2, 7));
	auto outflank_horizontal_left = p & (flipped_horizontal_left << 1);
	auto outflank_vertical_left = p & (flipped_vertical_left << 8);

	auto outflank_diag_right_2 = _mm_and_si128(p_2, _mm_slli_epi64(flipped_diag_right_2, 9));
	auto outflank_horizontal_right = p & (flipped_horizontal_right >> 1);
	auto outflank_vertical_right = p & (flipped_vertical_right >> 8);

#ifdef USE_SSE41
	flipped_diag_left_2 = _mm_andnot_si128(_mm_cmpeq_epi64(outflank_diag_left_2, _mm_setzero_si128()), flipped_diag_left_2);
	flipped_horizontal_left &= -(int)(outflank_horizontal_left != 0);
	flipped_vertical_left &= -(int)(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_andnot_si128(_mm_cmpeq_epi64(outflank_diag_right_2, _mm_setzero_si128()), flipped_diag_right_2);
	flipped_horizontal_right &= -(int)(outflank_horizontal_right != 0);
	flipped_vertical_right &= -(int)(outflank_vertical_right != 0);
#else
	flipped_diag_left_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_left_2), _mm_setzero_pd())), flipped_diag_left_2);
	flipped_horizontal_left &= -(int)(outflank_horizontal_left != 0);
	flipped_vertical_left &= -(int)(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_right_2), _mm_setzero_pd())), flipped_diag_right_2);
	flipped_horizontal_right &= -(int)(outflank_horizontal_right != 0);
	flipped_vertical_right &= -(int)(outflank_vertical_right != 0);
#endif
	auto flipped_2 = _mm_or_si128(flipped_diag_left_2, flipped_diag_right_2);
	auto flipped = flipped_horizontal_left | flipped_horizontal_right | flipped_vertical_left | flipped_vertical_right;
#ifdef USE_X64
	flipped |= _mm_cvtsi128_si64(flipped_2) | _byteswap_uint64(_mm_cvtsi128_si64(_mm_unpackhi_epi64(flipped_2, flipped_2)));
#else
	uint64_t data[2];
	std::memcpy(data, &flipped_2, 16);
	flipped |= data[0] | _byteswap_uint64(data[1]);
#endif
	return flipped;
}

/**
 * @fn
 * @brief Calculates mobility bit pattern with SSE.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @return Mobility disc pattern.
 * @sa calc_mobility_CPU
 * @detail
 * The mobility bit pattern is calculated using parallel prefix algorithm.
 * The mobilitity in 2 directions are calculated in parallel using SSE, in the meantime,
 * the mobility in the other 2 directions are calculated using CPU.
 *
 * Term explanation:
 * Mobility is the places where the player can put a disc.
 *
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility (Japanese)
*/
inline uint64_t Board::calc_mobility_SSE(uint64_t p, uint64_t o)
{
	auto p_2 = _mm_set_epi64x(_byteswap_uint64(p), p);
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;
	auto masked_o_2 = _mm_set_epi64x(_byteswap_uint64(masked_o), masked_o);

	auto flip_diag_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(p_2, 7));
	auto flip_horizontal = masked_o & (p << 1);
	auto flip_vertical = o & (p << 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flip_diag_2, 7)));
	flip_horizontal |= masked_o & (flip_horizontal << 1);
	flip_vertical |= o & (flip_vertical << 8);

	auto prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 7));
	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_vertical = o & (o << 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 14)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 14)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	auto mobility_2 = _mm_slli_epi64(flip_diag_2, 7);
	auto mobility = (flip_horizontal << 1) | (flip_vertical << 8);

	flip_diag_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(p_2, 9));
	flip_horizontal = masked_o & (p >> 1);
	flip_vertical = o & (p >> 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flip_diag_2, 9)));
	flip_horizontal |= masked_o & (flip_horizontal >> 1);
	flip_vertical |= o & (flip_vertical >> 8);

	prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 9));
	prefix_horizontal >>= 1;
	prefix_vertical >>= 8;

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 18)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 18)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	mobility_2 = _mm_or_si128(mobility_2, _mm_slli_epi64(flip_diag_2, 9));
	mobility |= (flip_horizontal >> 1) | (flip_vertical >> 8);

#ifdef USE_X64
	mobility |= _mm_cvtsi128_si64(mobility_2) | _byteswap_uint64(_mm_cvtsi128_si64(_mm_unpackhi_epi64(mobility_2, mobility_2)));
#else
	uint64_t data[2];
	std::memcpy(data, &mobility_2, 16);
	mobility |= data[0] | _byteswap_uint64(data[1]);
#endif
	return mobility & ~(p | o);
}

#else

/**
 * @fn
 * @brief Calculates flipped discs bit pattern with bit manipulation.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @param (coord) The coordinate where disc is put.
 * @return Flipped discs bit pattern.
 * @detail
 * The flipped discs bit pattern is calculated using parallel prefix algorithm.
 *
 * Term explanation:
 * Outflank are discs which surround the opponent discs.
 * 
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#movegen (Japanese)
*/
inline uint64_t Board::calc_flipped_discs_CPU(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;

	// left
	auto flipped_horizontal = masked_o & (coord_bit << 1);
	auto flipped_diag_A1H8 = masked_o & (coord_bit << 9);
	auto flipped_diag_A8H1 = masked_o & (coord_bit << 7);
	auto flipped_vertical = o & (coord_bit << 8);

	flipped_horizontal |= masked_o & (flipped_horizontal << 1);
	flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 << 9);
	flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 << 7);
	flipped_vertical |= o & (flipped_vertical << 8);

	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_diag_A1H8 = masked_o & (masked_o << 9);
	auto prefix_diag_A8H1 = masked_o & (masked_o << 7);
	auto prefix_vertical = o & (o << 8);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

	auto outflank_horizontal = p & (flipped_horizontal << 1);
	auto outflank_diag_A1H8 = p & (flipped_diag_A1H8 << 9);
	auto outflank_diag_A8H1 = p & (flipped_diag_A8H1 << 7);
	auto outflank_vertical = p & (flipped_vertical << 8);

	flipped_horizontal &= -(int)(outflank_horizontal != 0);
	flipped_diag_A1H8 &= -(int)(outflank_diag_A1H8 != 0);
	flipped_diag_A8H1 &= -(int)(outflank_diag_A8H1 != 0);
	flipped_vertical &= -(int)(outflank_vertical != 0);

	auto flipped = flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;

	// right
	flipped_horizontal = masked_o & (coord_bit >> 1);
	flipped_diag_A1H8 = masked_o & (coord_bit >> 9);
	flipped_diag_A8H1 = masked_o & (coord_bit >> 7);
	flipped_vertical = o & (coord_bit >> 8);

	flipped_horizontal |= masked_o & (flipped_horizontal >> 1);
	flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 >> 9);
	flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 >> 7);
	flipped_vertical |= o & (flipped_vertical >> 8);

	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_diag_A1H8 = masked_o & (masked_o >> 9);
	prefix_diag_A8H1 = masked_o & (masked_o >> 7);
	prefix_vertical = o & (o >> 8);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

	auto outflank_horizontal_right = p & (flipped_horizontal >> 1);
	auto outflank_diag_A1H8_right = p & (flipped_diag_A1H8 >> 9);
	auto outflank_diag_A8H1_right = p & (flipped_diag_A8H1 >> 7);
	auto outflank_vertical_right = p & (flipped_vertical >> 8);

	flipped_horizontal &= -(int)(outflank_horizontal_right != 0);
	flipped_diag_A1H8 &= -(int)(outflank_diag_A1H8_right != 0);
	flipped_diag_A8H1 &= -(int)(outflank_diag_A8H1_right != 0);
	flipped_vertical &= -(int)(outflank_vertical_right != 0);

	return flipped | flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;
}

/**
 * @fn
 * @brief Calculates mobility bit pattern.
 * @param (p) Current player's discs bit pattern.
 * @param (o) Opponent player's discs bit pattern.
 * @return Mobility disc pattern.
 * @detail
 * The mobility bit pattern is calculated using parallel prefix algorithm.
 *
 * Term explanation:
 * Mobility is the places where the player can put a disc. 
 *
 * @cite
 * https://www.chessprogramming.org/Parallel_Prefix_Algorithms
 * http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility (Japanese)
*/
inline uint64_t Board::calc_mobility_CPU(uint64_t p, uint64_t o)
{
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;

	// left
	auto flip_horizontal = masked_o & (p << 1);
	auto flip_diag_A1H8 = masked_o & (p << 9);
	auto flip_diag_A8H1 = masked_o & (p << 7);
	auto flip_vertical = o & (p << 8);

	flip_horizontal |= masked_o & (flip_horizontal << 1);
	flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 << 9);
	flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 << 7);
	flip_vertical |= o & (flip_vertical << 8);

	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_diag_A1H8 = masked_o & (masked_o << 9);
	auto prefix_diag_A8H1 = masked_o & (masked_o << 7);
	auto prefix_vertical = o & (o << 8);

	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	auto mobility = (flip_horizontal << 1) | (flip_diag_A1H8 << 9) | (flip_diag_A8H1 << 7) | (flip_vertical << 8);

	// right
	flip_horizontal = masked_o & (p >> 1);
	flip_diag_A1H8 = masked_o & (p >> 9);
	flip_diag_A8H1 = masked_o & (p >> 7);
	flip_vertical = o & (p >> 8);

	flip_horizontal |= masked_o & (flip_horizontal >> 1);
	flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 >> 9);
	flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 >> 7);
	flip_vertical |= o & (flip_vertical >> 8);

	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_diag_A1H8 = masked_o & (masked_o >> 9);
	prefix_diag_A8H1 = masked_o & (masked_o >> 7);
	prefix_vertical = o & (o >> 8);

	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	mobility |= (flip_horizontal >> 1) | (flip_diag_A1H8 >> 9) | (flip_diag_A8H1 >> 7) | (flip_vertical >> 8);
	return mobility & ~(p | o);
}

#endif

#if defined(USE_AVX2) || defined(USE_SSE41) 

/**
 * @fn
 * @brief Calculates hash code of discs' position.
 * @return Hash code.
 * @detail
 * The hash code is calculated using Zobrist's hash algorithm.
 * The hash code is the xor sum of HASH_RANK.
 * HASH_RANK has random bit patterns correspond to each rank(horizontal line). 
 * HASH_RANK array is initialized in Board::static_initializer function.
 * This is optimized function using SSE2. 
 * The original code is from [1].
 *
 * @cite
 * [1] https://github.com/okuhara/edax-reversi-AVX/blob/6514f8afa6ffeb8fe0305094fd2a89275848f048/src/board_sse.c#L779.
 */
uint64_t Board::calc_hash_code_SSE()
{
	auto rank = (byte*)&this->bitboard;

	__m128	h_0, h_1, h_2, h_3;

	h_0 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[0][rank[0]])), (__m64*) & hash_rank[4][rank[4]]);
	h_1 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[1][rank[1]])), (__m64*) & hash_rank[5][rank[5]]);
	h_2 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[2][rank[2]])), (__m64*) & hash_rank[6][rank[6]]);
	h_3 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[3][rank[3]])), (__m64*) & hash_rank[7][rank[7]]);
	h_0 = _mm_xor_ps(h_0, h_2);	h_1 = _mm_xor_ps(h_1, h_3);
	h_2 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[8][rank[8]])), (__m64*) & hash_rank[10][rank[10]]);
	h_3 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[9][rank[9]])), (__m64*) & hash_rank[11][rank[11]]);
	h_0 = _mm_xor_ps(h_0, h_2);	h_1 = _mm_xor_ps(h_1, h_3);
	h_2 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[12][rank[12]])), (__m64*) & hash_rank[14][rank[14]]);
	h_3 = _mm_loadh_pi(_mm_castsi128_ps(_mm_loadl_epi64((__m128i*) & hash_rank[13][rank[13]])), (__m64*) & hash_rank[15][rank[15]]);
	h_0 = _mm_xor_ps(h_0, h_2);	h_1 = _mm_xor_ps(h_1, h_3);
	h_0 = _mm_xor_ps(h_0, h_1);
	h_0 = _mm_xor_ps(h_0, _mm_movehl_ps(h_1, h_0));

#ifdef USE_X64
	return _mm_cvtsi128_si64(_mm_castps_si128(h_0));
#else
	uint64_t hash_code;
	std::memcpy(&hash_code, &h_0, 8);
	return hash_code;
#endif
}

#else

/**
 * @fn
 * @brief Calculates hash code of discs' position.
 * @return Hash code.
 * @detail
 * The hash code is calculated using Zobrist's hash algorithm.
 * The hash code is the xor sum of HASH_RANK.
 * HASH_RANK has random bit patterns correspond to each rank(horizontal line).
 * HASH_RANK array is initialized in Board::static_initializer function.
 * This is optimized function using SSE2.
 * The original code is from [1].
 *
 * @cite
 * [1] https://github.com/abulmo/edax-reversi/blob/1ae7c9fe5322ac01975f1b3196e788b0d25c1e10/src/board.c#L1106
 */
uint64_t Board::calc_hash_code_CPU()
{
	unsigned long long h_1, h_2;
	const auto rank = (byte*)&this->bitboard;

	h_1 = hash_rank[0][rank[0]];
	h_2 = hash_rank[1][rank[1]];
	h_1 ^= hash_rank[2][rank[2]];
	h_2 ^= hash_rank[3][rank[3]];
	h_1 ^= hash_rank[4][rank[4]];
	h_2 ^= hash_rank[5][rank[5]];
	h_1 ^= hash_rank[6][rank[6]];
	h_2 ^= hash_rank[7][rank[7]];
	h_1 ^= hash_rank[8][rank[8]];
	h_2 ^= hash_rank[9][rank[9]];
	h_1 ^= hash_rank[10][rank[10]];
	h_2 ^= hash_rank[11][rank[11]];
	h_1 ^= hash_rank[12][rank[12]];
	h_2 ^= hash_rank[13][rank[13]];
	h_1 ^= hash_rank[14][rank[14]];
	h_2 ^= hash_rank[15][rank[15]];

	return h_1 ^ h_2;
}

#endif