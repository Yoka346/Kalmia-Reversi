 #pragma once
#include "../pch.h"
#include <fstream>
#include <istream>
#include <ostream>
#include <sstream>
#include "../datetime.h"
#include "feature.h"
#include "../reversi/board.h"

namespace evaluation
{
	constexpr ConstantArray<int32_t, FEATURE_NUM> PATTERN_OFFSET(
		[](int* table, size_t length)
		{
			int32_t offset = 0;
			int32_t idx = 0;
			for (int32_t kind = 0; kind < FEATURE_KIND_NUM; kind++)
			{
				for (int32_t i = 0; i < EACH_FEATURE_NUM[kind]; i++)
					table[idx++] = offset;
				offset += PATTERN_NUM[kind];
			}
		}
	);

	constexpr size_t calc_feature_vector_len()
	{
		int32_t len = 0;
		for (int32_t kind = 0; kind < FEATURE_KIND_NUM; kind++)
			len += PATTERN_NUM[kind];
		return len;
	}

	constexpr size_t FEATURE_VEC_LEN = calc_feature_vector_len();
	constexpr size_t BIAS_IDX = calc_feature_vector_len() - 1;

	const ReadOnlyArray<int32_t, FEATURE_VEC_LEN> TO_OPPONENT_PATTERN_IDX(
		[](int32_t* table, size_t length)
		{
			int32_t offset = 0;
			int32_t idx = 0;
			for (int32_t kind = 0; kind < FEATURE_KIND_NUM; kind++)
			{
				auto max = PATTERN_NUM[kind];
				for (int32_t pat = 0; pat < max; pat++)
					table[offset + pat] = calc_opponent_pattern(pat, FEATURE_SIZE[kind]);
				offset += PATTERN_NUM[kind];
			}
		}
	);

	const ReadOnlyArray<int32_t, FEATURE_VEC_LEN> TO_SYMMETRIC_PATTERN_IDX(
		[](int32_t* table, size_t length)
		{
			int32_t offset = 0;
			int32_t idx = 0;
			for (int32_t kind = 0; kind < FEATURE_KIND_NUM; kind++)
			{
				auto max = PATTERN_NUM[kind];
				for (int32_t pat = 0; pat < max; pat++)
					table[offset + pat] = to_symmetric_pattern(static_cast<FeatureKind>(kind), pat);
				offset += PATTERN_NUM[kind];
			}
		}
	);

	constexpr int32_t BIAS_IDX = calc_feature_vector_len() - 1;

	typedef struct EvalParamsFileHeader
	{
	public:
		static constexpr int HEADER_SIZE = 29;

		EvalParamsFileHeader() { ; }
		EvalParamsFileHeader(const std::string& label, int32_t version, DateTime& releasedTime);
		EvalParamsFileHeader(const std::string path);
		EvalParamsFileHeader(std::ifstream& ifs);

		inline const std::string& get_label() const { return this->label; }
		inline int get_version() const { return this->version; }
		inline const DateTime& get_released_time() const { return this->released_time; }

		DLL_EXPORT void write_to_file(const std::string& path);
		DLL_EXPORT void write_to_stream(std::ostream& stream);

	private:
		static constexpr int LABEL_OFFSET = 0;
		static constexpr int LABEL_SIZE = 16;
		static constexpr int VERSION_OFFSET = LABEL_OFFSET + LABEL_SIZE;
		static constexpr int VERSION_SIZE = 4;
		static constexpr int DATE_TIME_OFFSET = VERSION_OFFSET + VERSION_SIZE;
		static constexpr int DATE_TIME_SIZE = 9;

		std::string label;
		int32_t version;
		DateTime released_time;

		static void load(const std::string path, char* buffer);  
		static void load(std::ifstream& ifs, char* buffer);
		static DateTime load_datetime(const char* buffer);
		static void datetime_to_buffer(DateTime& datetime, char* buffer);

		void init(const char* buffer);
	};

	/**
	 * @class
	 * @brief	Provides evaluation function. This evaluation function produces the estimated winning rate of game.
	 * @detail	This evaluation function calculates odds(winning_rate / (1 - winning_rate)) from the lenear sum of the weight of the appeared feature,
	 *			then inputs that estimated odds to standard sigmoid function to convert it to the winning rate.
	*/
	class EvalFunction
	{
	public:
		EvalFunction(const std::string& label, int32_t version, int32_t move_count_per_stage);
		EvalFunction(const std::string& path);
		EvalFunction(const EvalFunction& eval_func);
		~EvalFunction();

		inline EvalParamsFileHeader& get_header() { this->header; }
		inline int32_t get_stage_num() { this->stage_num; }
		inline int get_move_count_per_stage() { this->move_count_per_stage; }

		/*inline void init_weight_with_normal_rand() { init_weight_with_normal_rand(0.0f, 0.01f); }
		DLL_EXPORT void init_weight_with_normal_rand(float mu, float sigma);
		DLL_EXPORT void copy_black_params_to_symmetric_feature_idx();
		DLL_EXPORT void copy_black_params_to_white_params();
		DLL_EXPORT void save_to_file(const std::string& path);
		DLL_EXPORT void save_to_file(std::ofstream& ofs);*/
		DLL_EXPORT float f(BoardFeature& bf);
		DLL_EXPORT float f(int32_t stage, BoardFeature& bf);
		/*DLL_EXPORT float f_for_optimization(BoardFeature& bf);
		DLL_EXPORT float f_for_optimization(int stage, BoardFeature& bf);
		DLL_EXPORT float calc_grad(int stage, std::tuple<BoardFeature&, float>* batch, float* grad, int len);
		DLL_EXPORT void apply_grad_to_black_weight(int stage, float* grad, float* rate, int len);
		DLL_EXPORT float calc_loss(std::tuple<BoardFeature&, float>* batch, int len);*/

	private:
		EvalParamsFileHeader header;
		int32_t stage_num;
		int32_t move_count_per_stage;
		float** weight[2];

		std::shared_ptr<std::shared_ptr<std::shared_ptr<float>>> load_packed_weight(std::ifstream& ifs, int32_t& stage_num);
		void expand_packed_weight(std::shared_ptr<std::shared_ptr<std::shared_ptr<float>>> packed_weight);
	};
}