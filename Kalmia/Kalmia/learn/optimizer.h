#pragma once

#include "../io/logger.h"
#include "../evaluate/position_eval.h"

namespace learn
{
	struct ValueFuncOptimizerOptions
	{
		int32_t epoch_num = 1;
		float learning_rate_base = 0.1f;	// �w�K��. 
		float max_feature_occurrence_factor = 1.0e-2f;	// �����̏o�����ɉ����ĕω�����W���̍ő�l.
		float learning_rate_decay = 0.5f;	// �ߊw�K�������������̊w�K���̌�����. 
		int32_t checkpoint_interval = 5;	// ��epoch���Ƃɉߊw�K�`�F�b�N�ƍœK�p�����[�^�[�̕ۑ����s����.
		float tolerance = 1.0e-4f;		// ���̒l�ȉ��̃��X�̕ϓ��͋��e����.
		int32_t patience = 3;	// �ߊw�K���������Ă����̉񐔂����͌�����. �ꎞ�I�ɏオ����, �܂�������ꍇ������̂�.
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
