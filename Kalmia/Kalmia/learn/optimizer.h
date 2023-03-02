#pragma once

#include "../io/logger.h"
#include "../evaluate/position_eval.h"

namespace learn
{
	struct ValueFuncOptimizerOptions
	{
		int32_t epoch_num = 1;
		float learning_rate_base = 0.1f;	// 学習率. 
		float max_feature_occurrence_factor = 1.0e-2f;	// 特徴の出現数に応じて変化する係数の最大値.
		float learning_rate_decay = 0.5f;	// 過学習が発生した時の学習率の減衰率. 
		int32_t checkpoint_interval = 5;	// 何epochごとに過学習チェックと最適パラメーターの保存を行うか.
		float tolerance = 1.0e-4f;		// この値以下のロスの変動は許容する.
		int32_t patience = 3;	// 過学習が発生してもこの回数だけは見逃す. 一時的に上がって, また下がる場合もあるので.
		std::string work_dir_path;
		std::string train_data_path;
		std::string validation_data_path;
		std::string log_file_path;
	};

	template <evaluation::ValueRepresentation VALUE_REPS>
	class ValueFuncOptimizer
	{
	public:
		ValueFuncOptimizer(evaluation::ValueFunction<VALUE_REPS>& value_func, ValueFuncOptimizerOptions& options)
			:current_model(value_func), options(options), logger(options.log_file_path)
		{}

	private:
		evaluation::ValueFunction<VALUE_REPS> current_model;
		ValueFuncOptimizerOptions options;
		io::Logger logger;
	};
}
