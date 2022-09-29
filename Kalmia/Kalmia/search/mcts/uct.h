#pragma once

#include <vector>
#include <thread>
#include <chrono>

#include "../common.h"
#include "node.h"
#include "../../evaluate/position_eval.h"

namespace search::mcts
{
	struct UCTOptions
	{
		// �T���X���b�h��(�f�t�H���g��CPU�̘_���R�A��).
		int32_t thread_num = std::thread::hardware_concurrency();

		// ���������Node�I�u�W�F�N�g�̐�(Node::object_count())�̏��.
		uint64_t node_num_limit = 2e+7;

		// ���l�֐��̃p�����[�^�t�@�C���̏ꏊ.
		std::string value_func_param_file_path;
	};

	/**
	* @struct
	* @brief �T���̌��ʓ���ꂽ����̕]�����܂Ƃ߂�\����.
	**/
	struct MoveEvaluation
	{
		reversi::BoardCoordinate move;

		// ���̒���ɔ�₳�ꂽ�T���̊���. (���̒����playout_count) / (���̌����playout_count�̑��a) �ɓ�����.
		double effort;

		// ���̒���ɔ�₳�ꂽ�v���C�A�E�g(�����ł͉��l�֐��̌Ăяo�����w��)�̉�.
		uint32_t playout_count;

		// �T���̌��ʓ���ꂽ, ���̒��肩���̊��ҏ���(�����ɂ͉��l�̊��Ғl). 
		double expected_value;

		// ���l�֐��̏o��. ���Ȃ킿�T������؂����ɗ\����������(���l).
		double raw_value;

		// Principal Variation(�őP�����).
		std::vector<reversi::BoardCoordinate> pv;
	};

	struct SearchInfo
	{
		MoveEvaluation root_eval;
		utils::DynamicArray<MoveEvaluation> child_evals;
		bool early_stopping;
	};

	/**
	* @class
	* @brief UCT(Upper Confidence Tree, �M�������)��\���N���X.
	* @detail UCT�̓��[�g�m�[�h������, ���̃��[�g�m�[�h����؂��W�J����Ă���. �T���Ɋւ�鏈���͑S�Ă̂��̃N���X�̃����o�֐��Ƃ��Ď�������Ă���.
	**/
	class UCT
	{
	private:
		// ���[�g�m�[�h�����̎q�m�[�h��FPU(First Play Urgency).
		// FPU�͖��K��m�[�h�̊��ҕ�V�̏����l. ���[�g�m�[�h�����ȊO�̎q�m�[�h��, �e�m�[�h�̊��ҕ�V��FPU�Ƃ��ėp����.
		static constexpr float ROOT_FPU = 1.0f;

		// ToDo: UCB�Ɋւ��W���͏\���ɍœK���ł��Ă��Ȃ�����, �x�C�Y�œK���Ȃǂ�p���ă`���[�j���O����.

		// UCB���v�Z����ۂ̌W���̂�����1��. AlphaZero�ŗp���Ă��鎮��C_init�ɂ�����.
		static constexpr float UCB_FACTOR_INIT = 0.35f;

		// UCB���v�Z����ۂ̌W���̂�����1��. AlphaZero�ŗp���Ă��鎮��C_base�ɂ�����.
		static constexpr uint32_t UCB_FACTOR_BASE = 19652;

		// �����X���b�h�ŒT������ۂ�, ����̃m�[�h�ɒT�����W�����Ȃ��悤�ɂ��邽�߂�, �T�����̃m�[�h�ɗ^����ꎞ�I�ȃy�i���e�B.
		static constexpr uint32_t VIRTUAL_LOSS = 3;

		static constexpr Array<EdgeLabel, 3> GAME_RESULT_TO_EDGE_LABEL = { EdgeLabel::LOSS, EdgeLabel::DRAW, EdgeLabel::WIN };

		const UCTOptions OPTIONS;
		const evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> VALUE_FUNC;

		reversi::Position root_state;
		Node root;

		// pps(playout per second)���v�Z���邽�߂̃J�E���^�[.
		uint32_t pps_counter;

		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		bool search_stop_flag = true;
		bool _is_searching = false;

	public:
		UCT(UCTOptions& options) : OPTIONS(options), VALUE_FUNC(options.value_func_param_file_path) { ; }

		bool is_searching() { return this->_is_searching; }

		int32_t search_ellapsed_ms() 
		{ 
			using namespace std::chrono;
			if (this->_is_searching)
				return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_start_time).count();
			return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_end_time).count();
		}

		double pps() { return this->pps_counter / (this->search_ellapsed_ms() * 1.0e-3); }

		void set_root_state(const reversi::Position& pos);

		/**
		* @fn
		* @brief ���[�g�Ֆʂ𒅎�ɂ���Ď��̔ՖʂɑJ�ڂ�����.
		* @param (move) ����ʒu.
		* @return �J�ڂł�����true.
		**/
		bool transition_root_state_to_child_state(reversi::BoardCoordinate move);
	};
}