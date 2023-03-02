#pragma once

#include <vector>
#include <thread>
#include <future>
#include <chrono>
#include <numeric>

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

		// UCT::_search_info �̓��e���X�V����Ԋu.
		int32_t search_info_update_interval_cs = 0;

		// �T�����\�����ǂ����𔻒肷��ۂ�臒l(>= 1.0). ���̒l��傫������΂���قǒT���������������₷���Ȃ�. 
		double enough_search_threshold = 1.5;
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

		MoveEvaluation() 
			: move(reversi::BoardCoordinate::NULL_COORD), effort(0.0), playout_count(0u), 
			expected_reward(0.0), game_result(reversi::GameResult::NOT_OVER), pv() { ; }

		bool prior_to(const MoveEvaluation& move_eval) const;
	};

	struct SearchInfo
	{
		MoveEvaluation root_eval;
		std::vector<MoveEvaluation> child_evals;

		SearchInfo() : child_evals(0) { ; }
	};

	/**
	* @enum
	* @brief �T���I�����̃X�e�[�^�X.
	**/
	enum class SearchEndStatus : uint16_t
	{
		COMPLETE = 0x0001,	// �w�肳�ꂽ�v���C�A�E�g�񐔂����T�����s����.
		PROVED = 0x0002,		// ���s���m�肵��.
		TIMEOUT = 0x0004,	// �������Ԃ��}�������ߒT�����I������.
		SUSPENDED_BY_STOP_SIGNAL = 0x0008,	// UCT::send_stop_search_signal�֐��ɂ���ĒT�������f���ꂽ.
		OVER_NODES = 0x0010,	// �m�[�h�����K��l���I�[�o�[�������ߒT�������f���ꂽ.
		EARLY_STOPPING = 0x0020,		// �T���������I���̏����𖞂��������ߏI������.
		EXTENDED = 0x0f00	// �T�����������ꂽ.
	};

	inline SearchEndStatus operator&(const SearchEndStatus& left,  const SearchEndStatus& right) 
	{
		return static_cast<SearchEndStatus>(static_cast<uint16_t>(left) & static_cast<uint16_t>(right));
	}

	inline SearchEndStatus operator|(SearchEndStatus& left, const SearchEndStatus& right)
	{
		return static_cast<SearchEndStatus>(static_cast<uint16_t>(left) | static_cast<uint16_t>(right));
	}

	inline SearchEndStatus operator^(SearchEndStatus& left, const SearchEndStatus& right)
	{
		return static_cast<SearchEndStatus>(static_cast<uint16_t>(left) ^ static_cast<uint16_t>(right));
	}

	inline SearchEndStatus& operator|=(SearchEndStatus& left, const SearchEndStatus& right)
	{
		return left = left | right;
	}

	inline SearchEndStatus& operator^=(SearchEndStatus& left, const SearchEndStatus& right)
	{
		return left = left ^ right;
	}

	/**
	* @class
	* @Node�I�u�W�F�N�g�����b�N���邽�߂�mutex��񋟂���N���X.
	**/
	class MutexPool
	{
	public:
		/**
		* @fn
		* @brief �Ֆʂ��L�[�Ƃ���, �v�[������mutex���擾����.
		* @detail �ɋH�ɃL�[���Փ˂��邪, ���ʂȃ��b�N���������邾���Ȃ̂œ��ɖ��͂Ȃ�. 
		**/
		std::mutex& get(const reversi::Position& pos)
		{
			// pos.calc_hash_code() & (SIZE - 1) �� pos.calc_hash_code() % SIZE �Ɠ����Ӗ�. SIZE == 2^n �����琬�藧��.
			return this->pool[pos.calc_hash_code() & (SIZE - 1)];
		}

	private:
		static constexpr size_t SIZE = 1 << 16;	// �]��̌v�Z���y�ɂȂ�̂ŃT�C�Y��2^n�ɂ���.

		Array<std::mutex, SIZE> pool;
	};

	/**
	* @class
	* @brief UCT(Upper Confidence Tree, �M�������)��\���N���X.
	* @detail UCT�̓��[�g�m�[�h������, ���̃��[�g�m�[�h����؂��W�J����Ă���. �T���Ɋւ�鏈���͑S�Ă̂��̃N���X�̃����o�֐��Ƃ��Ď�������Ă���.
	**/
	class UCT
	{
	public:
		UCTOptions options;
		std::function<void(const SearchInfo&)> on_search_info_was_updated = [](const auto&) {};

		UCT(const std::string& value_func_param_file_path) : UCT(UCTOptions(), value_func_param_file_path) { ; }
		UCT(const UCTOptions& options, const std::string& value_func_param_file_path)
			: options(options), value_func(value_func_param_file_path), mutex_pool(), node_gc(),
			_node_count_per_thread(0), root_edge_label(EdgeLabel::NOT_PROVED), _search_info(), max_playout_count(0)
		{
			;
		}

		bool is_searching() { return this->_is_searching; }

		std::chrono::milliseconds search_ellapsed_ms()
		{
			using namespace std::chrono;
			if (this->_is_searching)
				return duration_cast<milliseconds>(high_resolution_clock::now() - this->search_start_time);
			return duration_cast<milliseconds>(this->search_end_time - this->search_start_time);
		}

		uint32_t node_count() { return std::accumulate(this->_node_count_per_thread.begin(), this->_node_count_per_thread.end(), 0); }
		double nps() { return node_count() / (this->search_ellapsed_ms().count() * 1.0e-3); }
		SearchInfo search_info() { this->search_info_mutex.lock(); auto tmp = this->_search_info; this->search_info_mutex.unlock(); return tmp; }

		bool early_stopping_is_enabled() const { return this->_early_stopping_is_enabled; }
		void enable_early_stopping() { this->_early_stopping_is_enabled = true; }
		void disable_early_stopping() { this->_early_stopping_is_enabled = false; }
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
		* @brief �T�����s��. �������Ԃ�(INT32_MAX / 10)[cs](��24.8��)
		* @param (playout_num) �v���C�A�E�g��(�����ł͑I��->�W�J->�]��->�o�b�N�A�b�v�̗�������s�����).
		**/
		SearchEndStatus search(uint32_t playout_num) { search(playout_num, INT32_MAX / 10, 0); }

		/**
		* @fn
		* @brief �T�����s��.
		* @param (playout_num) �v���C�A�E�g��(�����ł͑I��->�W�J->�]��->�o�b�N�A�b�v�̗�������s�����).
		* @param (time_limit_cs) ��������[cs]. 
		* @param (extra_time_cs) �T���������K�v�ȏꍇ�ɏ�����\������[cs].
		* @return �T���I���X�e�[�^�X.
		**/
		SearchEndStatus search(uint32_t playout_num, int32_t time_limit_cs, int32_t extra_time_cs);

		/**
		* @fn
		* @brief �Ăяo�����Ƃ͕ʃX���b�h�ŒT�����s��.
		**/
		std::future<SearchEndStatus> search_async(uint32_t playout_num, std::function<void (SearchEndStatus)> search_end_callback) { return search_async(playout_num, INT32_MAX / 10, 0, search_end_callback); }

		/**
		* @fn
		* @brief �Ăяo�����Ƃ͕ʃX���b�h�ŒT�����s��.
		* @param (playout_num) �v���C�A�E�g��.
		* @param (time_time_cs) ���Ԑ���[cs].
		* @param (extra_time_cs) �T���������K�v�ȏꍇ�ɏ�����\������[cs].
		* @param (search_end_callback) �T�����I�������ۂ�SearchEndStatus��n����ČĂяo�����R�[���o�b�N.
		* @return SearchEndStatus��future.
		**/
		std::future<SearchEndStatus> search_async(uint32_t playout_num, int32_t time_limit_cs, int32_t extra_time_cs, std::function<void(SearchEndStatus)> search_end_callback)
		{
			this->recieved_stop_search_signal = false;
			this->_is_searching = true;
			auto worker = [=, this]()
			{
				auto status = search(playout_num, time_limit_cs, extra_time_cs);
				recieved_stop_search_signal = false;
				search_end_callback(status);
				return status;
			};
			return std::async(worker);
		}

		void send_stop_search_signal() { if (this->_is_searching) this->recieved_stop_search_signal = true; }

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

		static constexpr Array<float, 3> GAME_RESULT_TO_REWARD = { 1.0f, 0.0f, 0.5f };

		evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> value_func;

		MutexPool mutex_pool;
		NodeGarbageCollector node_gc;

		reversi::Position root_state;
		std::unique_ptr<Node> root;
		EdgeLabel root_edge_label;

		// �X���b�h���Ƃ̃m�[�h��. ���K��m�[�h�ɏ��߂ĖK�₵���Ƃ��ɉ��Z�����. ������, �O��̒T����������p�����m�[�h�͌v�Z�ɓ���Ă��Ȃ�.
		std::vector<uint32_t> _node_count_per_thread;

		// �T�����J�n���Ă�����s���ꂽ�v���C�A�E�g��. ���̒l�́@�I��->�W�J->�]��->�o�b�N�A�b�v �̗����1��I��������Z�����.
		std::atomic<uint32_t> playout_count;

		// �v���C�A�E�g���̍ő�l.
		uint32_t max_playout_count;

		SearchInfo _search_info;
		std::mutex search_info_mutex;
		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		bool _early_stopping_is_enabled = true;
		bool stop_search_flag = false;
		std::atomic<bool> recieved_stop_search_signal = false;
		std::atomic<bool> _is_searching = false;

		void init_root_child_nodes();

		/**
		* @fn
		* @detail �T�����[�J�[. �T���X���b�h����������ɂ��̊֐������s�����.
		**/
		void search_worker(int32_t thread_id, GameInfo& game_info);
		SearchEndStatus wait_for_search(std::vector<std::thread>& search_threads, std::chrono::milliseconds time_limit_ms, std::chrono::milliseconds extra_time_ms);

		void visit_root_node(int32_t thread_id, GameInfo& game_info);

		template<bool AFTER_PASS>
		float visit_node(int32_t thread_id, GameInfo& game_info, Node* current_node, Edge& edge_to_current_node);

		int32_t select_root_child_node();
		int32_t select_child_node(Node* parent, Edge& edge_to_parent);

		float predict_reward(GameInfo& game_info) { return 1.0f - this->value_func.predict(game_info.feature()); }

		/**
		* @fn
		* @brief �e�m�[�h�ƑI�����ꂽ�ӂɕ�V��t�^����, virtual loss����菜��.
		**/
		void update_statistic(Node* node, Edge& edge, double reward)	// �ق��̌��ł͕�V��float�^����, ���Z����double�ɂ���.
		{
			if constexpr (VIRTUAL_LOSS != 1)
			{
				node->visit_count.fetch_sub(VIRTUAL_LOSS - 1, std::memory_order_acq_rel);
				edge.visit_count.fetch_sub(VIRTUAL_LOSS - 1, std::memory_order_acq_rel);
			}
			edge.reward_sum.fetch_add(reward, std::memory_order_acq_rel);
		}

		/**
		* @fn
		* @brief �p�X�m�[�h�p��update_statistic�֐�. virtual loss����菜���������Ȃ���Ă���.
		**/
		void update_pass_node_statistic(Node* node, Edge& edge, double reward)
		{
			node->visit_count.fetch_add(1, std::memory_order_acq_rel);
			edge.visit_count.fetch_add(1, std::memory_order_acq_rel);
			edge.reward_sum.fetch_add(reward, std::memory_order_acq_rel);
		}

		void add_virtual_loss(Node* node, Edge& edge)
		{
			node->visit_count.fetch_add(VIRTUAL_LOSS, std::memory_order_acq_rel);
			edge.visit_count.fetch_add(VIRTUAL_LOSS, std::memory_order_acq_rel);
		}

		void get_top2_edges(Edge*& best, Edge*& second_best);

		bool can_stop_search(std::chrono::milliseconds time_limit_ms, SearchEndStatus& end_status);

		bool can_do_early_stopping(std::chrono::milliseconds time_limit_ms);

		bool extra_search_is_needed();

		void update_search_info();

		/**
		* @fn
		* @brief Principal Variation(�őP�����)���擾����.
		* @detail UCT�ɂ����Ă�, �K��񐔂������m�[�h�͗L�]�ȃm�[�h�Ȃ̂�, ��{�I�ɂ͖K��񐔂̑����m�[�h�����[�t�m�[�h�Ɏ���܂őI�ё�����.
		* �m�[�h�̑I�ѕ��̏ڍ�:
		* 1. �K��񐔂��ł������m�[�h��I��. ������, ���ꂪ2�ȏ゠�����ꍇ��, ���l���ł������m�[�h��I��.
		* 2. �����m��m�[�h������ΖK��񐔂Ɋւ�炸, ���̃m�[�h��I��. 
		* 3. �K��񐔂Ɋւ�炸, �s�k�m��m�[�h�͑I�΂Ȃ�. ������, �s�k�m��m�[�h�����Ȃ��ꍇ�͑I�Ԃ����Ȃ�.
		* 4. ���������m��m�[�h�Ɣs�k�m��m�[�h�����Ȃ��ꍇ��, ���������m��m�[�h��I��.
		**/
		void get_pv(Node* root, std::vector<reversi::BoardCoordinate>& pv);
	};

	template float UCT::visit_node<true>(int32_t, GameInfo&, Node*, Edge&);
	template float UCT::visit_node<false>(int32_t, GameInfo&, Node*, Edge&);
}