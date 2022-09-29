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
		std::atomic<uint32_t> pps_counter;

		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		std::atomic<bool> search_stop_flag = true;
		bool _is_searching = false;

		void init_root_child_nodes();

		/**
		* @fn
		* @detail �T�����[�J�[. �T���X���b�h����������ɂ��̊֐������s�����.
		**/
		void search_kernel(GameInfo& game_info, uint32_t playout_num, int32_t time_limit_ms);

		void visit_root_node(GameInfo& game_info);

		template<bool AFTER_PASS>
		double visit_node(GameInfo& game_info, Node* current_node, Edge& edge_to_current_node);

		int32_t select_root_child_node();
		int32_t select_child_node();

		float playout(GameInfo& game_info)
		{
			this->pps_counter++;
			return 1.0f - this->VALUE_FUNC.predict(game_info.feature());
		}

		/**
		* @fn
		* @brief �e�m�[�h�ƑI�����ꂽ�ӂɕ�V��t�^����, virtual loss����菜��.
		**/
		void update_statistic(Node* node, Edge& edge, double reward)
		{
			if constexpr (VIRTUAL_LOSS != 1)
			{
				node->visit_count += 1 - VIRTUAL_LOSS;
				edge.visit_count += 1 - VIRTUAL_LOSS;
			}
			edge.reward_sum += reward;
		}

		void add_virtual_loss(Node* node, Edge& edge)
		{
			node->visit_count += VIRTUAL_LOSS;
			edge.visit_count += VIRTUAL_LOSS;
		}

		/**
		* @fn
		* @brief Principal Variation(�őP�����)���擾����.
		* @detail UCT�ɂ����Ă�, �K��񐔂������m�[�h�͗L�]�ȃm�[�h�Ȃ̂�, ��{�I�ɂ͖K��񐔂̑����m�[�h��؂̖��[�Ɏ���܂őI�ё�����.
		* �m�[�h�̑I�ѕ��̏ڍ�:
		* 1. �K��񐔂��ł������m�[�h��I��. ������, ���ꂪ2�ȏ゠�����ꍇ��, ���l���ł������m�[�h��I��.
		* 2. �����m��m�[�h������ΖK��񐔂Ɋւ�炸, ���̃m�[�h��I��. ������, �����m��m�[�h���������݂���ꍇ��, �ŏI�΍����ő�̃m�[�h��I��.
		* 3. �ł��K��񐔂̑����m�[�h���s�k�m��m�[�h�ł����, ���ɖK��񐔂̑����m�[�h��I��.
		* 4. ���������m��m�[�h�ƕ����m��m�[�h�����Ȃ��ꍇ��, ���������m��m�[�h��I��.
		* 5. �s�k�m��m�[�h���������ꍇ��, �ŏI�΍����ŏ��̃m�[�h��I��.
		**/
		void get_pv(Node* root, std::vector<reversi::BoardCoordinate> pv);

	public:
		UCT(UCTOptions& options) : OPTIONS(options), VALUE_FUNC(options.value_func_param_file_path), pps_counter(0) { ; }

		bool is_searching() { return this->_is_searching; }

		int32_t search_ellapsed_ms() 
		{ 
			using namespace std::chrono;
			if (this->_is_searching)
				return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_start_time).count();
			return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_end_time).count();
		}

		double pps() { return this->pps_counter / (this->search_ellapsed_ms() * 1.0e-3); }

		void get_search_info(SearchInfo&& search_info);
		void set_root_state(const reversi::Position& pos);

		/**
		* @fn
		* @brief ���[�g�Ֆʂ𒅎�ɂ���Ď��̔ՖʂɑJ�ڂ�����.
		* @param (move) ����ʒu.
		* @return �J�ڂł�����true.
		**/
		bool transition_root_state_to_child_state(reversi::BoardCoordinate move);

		/**
		* @fn
		* @brief �T�����s��. �������Ԃ�INT32_MAX[ms](��24.8��)
		* @param (playout_num) �v���C�A�E�g��(�����ł͑I��->�W�J->�]��->�o�b�N�A�b�v�̗�������s�����).
		**/
		void search(uint32_t playout_num) { search(playout_num, INT32_MAX); }
		void search(uint32_t playout_num, int32_t time_limit_ms);
		void send_stop_search_signal() { if (this->_is_searching) this->search_stop_flag.store(true); }
	};

	template double UCT::visit_node<true>(GameInfo&, Node*, Edge&);
	template double UCT::visit_node<false>(GameInfo&, Node*, Edge&);
}