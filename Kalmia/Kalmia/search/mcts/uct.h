#pragma once

#include <vector>
#include <thread>
#include <chrono>

#include "../common.h"
#include "node.h"
#include "gc.h"
#include "../../evaluate/position_eval.h"

namespace search::mcts
{
	struct UCTOptions
	{
		// �T���X���b�h��(�f�t�H���g��CPU�̘_���R�A��).
		int32_t thread_num = std::thread::hardware_concurrency();

		// ���������Node�I�u�W�F�N�g�̐�(Node::object_count())�̏��.
		uint64_t node_num_limit = static_cast<uint64_t>(2e+7);

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

		// ���̒���ɔ�₳�ꂽ�v���C�A�E�g�̉�.
		uint32_t playout_count;

		// �T���̌��ʓ���ꂽ, ���̒��肩���̊��ҏ���(���ҕ�V). 
		double expected_reward;

		// ���s���m�肵�Ă���ꍇ�̌���.
		reversi::GameResult game_result;

		// Principal Variation(�őP�����).
		std::vector<reversi::BoardCoordinate> pv;

		MoveEvaluation() { ; }
	};

	struct SearchInfo
	{
		MoveEvaluation root_eval;
		utils::DynamicArray<MoveEvaluation> child_evals;

		SearchInfo() : child_evals(0) { ; }
	};

	/**
	* @class
	* @Node�I�u�W�F�N�g�����b�N���邽�߂�mutex��񋟂���N���X.
	**/
	class MutexPool
	{
	private:
		static constexpr size_t SIZE = 1 << 16;	// �]��̌v�Z���y�ɂȂ�̂ŃT�C�Y��2^n�ɂ���.

		Array<std::mutex, SIZE> pool;

	public:
		/**
		* @fn
		* @brief �Ֆʂ��L�[�Ƃ���, �v�[������mutex���擾����.
		* @detail �ɁX�H�ɃL�[���Փ˂��邪, ���ʂȃ��b�N���������邾���Ȃ̂œ��ɖ��͂Ȃ�. �ނ���Փˉ���̏��������ޕ������R�X�g.
		**/
		std::mutex& get(const reversi::Position& pos)
		{
			// pos.calc_hash_code() & (SIZE - 1) �� pos.calc_hash_code() % SIZE �Ɠ����Ӗ�. SIZE == 2^n �����琬�藧��.
			return this->pool[pos.calc_hash_code() & (SIZE - 1)];
		}
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

		// �����X���b�h�ŒT������ۂ�, ����̃m�[�h�ɒT�����W�����Ȃ��悤�ɂ��邽�߂ɒT�����̃m�[�h�ɗ^����ꎞ�I�ȃy�i���e�B.
		static constexpr int32_t VIRTUAL_LOSS = 3;

		static constexpr Array<double, 3> EDGE_LABEL_TO_REWARD = { 1.0, 0.0, 0.5 };

		const UCTOptions OPTIONS;
		const evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> VALUE_FUNC;

		MutexPool mutex_pool;
		NodeGarbageCollector node_gc;

		reversi::Position root_state;
		std::unique_ptr<Node> root;
		EdgeLabel root_edge_label;

		// pps(playout per second)���v�Z���邽�߂̃J�E���^�[.
		std::atomic<uint32_t> pps_counter;

		SearchInfo _search_info;
		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		std::atomic<bool> search_stop_flag = true;
		std::atomic<bool> _is_searching = false;

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
		int32_t select_child_node(Node* parent, Edge& edge_to_parent);

		float predict_reward(GameInfo& game_info)
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
		* @detail UCT�ɂ����Ă�, �K��񐔂������m�[�h�͗L�]�ȃm�[�h�Ȃ̂�, ��{�I�ɂ͖K��񐔂̑����m�[�h�����[�t�m�[�h�Ɏ���܂őI�ё�����.
		* �m�[�h�̑I�ѕ��̏ڍ�:
		* 1. �K��񐔂��ł������m�[�h��I��. ������, ���ꂪ2�ȏ゠�����ꍇ��, ���l���ł������m�[�h��I��.
		* 2. �����m��m�[�h������ΖK��񐔂Ɋւ�炸, ���̃m�[�h��I��. 
		* 3. �ł��K��񐔂̑����m�[�h���s�k�m��m�[�h�ł����, ���ɖK��񐔂̑����m�[�h��I��.
		* 4. ���������m��m�[�h�Ɣs�k�m��m�[�h�����Ȃ��ꍇ��, ���������m��m�[�h��I��.
		**/
		void get_pv(Node* root, std::vector<reversi::BoardCoordinate>& pv);
		void collect_search_result();

	public:
		UCT(UCTOptions& options) : OPTIONS(options), VALUE_FUNC(options.value_func_param_file_path), mutex_pool(), node_gc(), pps_counter(0) { ; }

		bool is_searching() { return this->_is_searching; }

		uint64_t search_ellapsed_ms() 
		{ 
			using namespace std::chrono;
			if (this->_is_searching.load())
				return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_start_time).count();
			return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_end_time).count();
		}

		double pps() { return this->pps_counter / (this->search_ellapsed_ms() * 1.0e-3); }
		const SearchInfo& search_info() { return this->_search_info; }

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

#ifdef _DEBUG
		void search_on_single_thread(uint32_t playout_num, int32_t time_limit_ms);
#endif

		void send_stop_search_signal() { if (this->_is_searching) this->search_stop_flag.store(true); }
	};

	template double UCT::visit_node<true>(GameInfo&, Node*, Edge&);
	template double UCT::visit_node<false>(GameInfo&, Node*, Edge&);
}