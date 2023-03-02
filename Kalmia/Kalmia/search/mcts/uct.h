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
		// 探索スレッド数(デフォルトはCPUの論理コア数).
		int32_t thread_num = std::thread::hardware_concurrency();

		// メモリ上のNodeオブジェクトの数(Node::object_count())の上限.
		uint64_t node_num_limit = static_cast<uint64_t>(2e+7);

		// UCT::_search_info の内容を更新する間隔.
		int32_t search_info_update_interval_cs = 0;

		// 探索が十分かどうかを判定する際の閾値(>= 1.0). この値を大きくすればするほど探索延長が発生しやすくなる. 
		double enough_search_threshold = 1.5;
	};

	/**
	* @struct
	* @brief 探索の結果得られた着手の評価をまとめる構造体.
	**/
	struct MoveEvaluation
	{
		reversi::BoardCoordinate move;

		// この着手に費やされた探索の割合. (この着手のplayout_count) / (他の候補手のplayout_countの総和) に等しい.
		double effort;

		// この着手に費やされたプレイアウトの回数.
		uint32_t playout_count;

		// 探索の結果得られた, この着手から先の期待勝率(期待報酬). 
		double expected_reward;

		// 勝敗が確定している場合の結果.
		reversi::GameResult game_result;

		// Principal Variation(最善応手列).
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
	* @brief 探索終了時のステータス.
	**/
	enum class SearchEndStatus : uint16_t
	{
		COMPLETE = 0x0001,	// 指定されたプレイアウト回数だけ探索を行った.
		PROVED = 0x0002,		// 勝敗が確定した.
		TIMEOUT = 0x0004,	// 制限時間を迎えたため探索を終了した.
		SUSPENDED_BY_STOP_SIGNAL = 0x0008,	// UCT::send_stop_search_signal関数によって探索が中断された.
		OVER_NODES = 0x0010,	// ノード数が規定値をオーバーしたため探索が中断された.
		EARLY_STOPPING = 0x0020,		// 探索が早期終了の条件を満たしたため終了した.
		EXTENDED = 0x0f00	// 探索が延長された.
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
	* @Nodeオブジェクトをロックするためのmutexを提供するクラス.
	**/
	class MutexPool
	{
	public:
		/**
		* @fn
		* @brief 盤面をキーとして, プールからmutexを取得する.
		* @detail 極稀にキーが衝突するが, 無駄なロックが発生するだけなので特に問題はない. 
		**/
		std::mutex& get(const reversi::Position& pos)
		{
			// pos.calc_hash_code() & (SIZE - 1) は pos.calc_hash_code() % SIZE と同じ意味. SIZE == 2^n だから成り立つ.
			return this->pool[pos.calc_hash_code() & (SIZE - 1)];
		}

	private:
		static constexpr size_t SIZE = 1 << 16;	// 余剰の計算が楽になるのでサイズは2^nにする.

		Array<std::mutex, SIZE> pool;
	};

	/**
	* @class
	* @brief UCT(Upper Confidence Tree, 信頼上限木)を表すクラス.
	* @detail UCTはルートノードを持ち, そのルートノードから木が展開されていく. 探索に関わる処理は全てのこのクラスのメンバ関数として実装されている.
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
		* @brief ルート盤面を着手によって次の盤面に遷移させる.
		* @param (move) 着手位置.
		* @return 遷移できたらtrue.
		**/
		bool transition_root_state_to_child_state(reversi::BoardCoordinate move);

		/**
		* @fn
		* @brief 探索を行う. 制限時間は(INT32_MAX / 10)[cs](約24.8日)
		* @param (playout_num) プレイアウト回数(ここでは選択->展開->評価->バックアップの流れを実行する回数).
		**/
		SearchEndStatus search(uint32_t playout_num) { search(playout_num, INT32_MAX / 10, 0); }

		/**
		* @fn
		* @brief 探索を行う.
		* @param (playout_num) プレイアウト回数(ここでは選択->展開->評価->バックアップの流れを実行する回数).
		* @param (time_limit_cs) 制限時間[cs]. 
		* @param (extra_time_cs) 探索延長が必要な場合に消費される予備時間[cs].
		* @return 探索終了ステータス.
		**/
		SearchEndStatus search(uint32_t playout_num, int32_t time_limit_cs, int32_t extra_time_cs);

		/**
		* @fn
		* @brief 呼び出し側とは別スレッドで探索を行う.
		**/
		std::future<SearchEndStatus> search_async(uint32_t playout_num, std::function<void (SearchEndStatus)> search_end_callback) { return search_async(playout_num, INT32_MAX / 10, 0, search_end_callback); }

		/**
		* @fn
		* @brief 呼び出し側とは別スレッドで探索を行う.
		* @param (playout_num) プレイアウト数.
		* @param (time_time_cs) 時間制限[cs].
		* @param (extra_time_cs) 探索延長が必要な場合に消費される予備時間[cs].
		* @param (search_end_callback) 探索が終了した際にSearchEndStatusを渡されて呼び出されるコールバック.
		* @return SearchEndStatusのfuture.
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
		// ルートノード直下の子ノードのFPU(First Play Urgency).
		// FPUは未訪問ノードの期待報酬の初期値. ルートノード直下以外の子ノードは, 親ノードの期待報酬をFPUとして用いる.
		static constexpr float ROOT_FPU = 1.0f;

		// ToDo: UCBに関わる係数は十分に最適化できていないため, ベイズ最適化などを用いてチューニングする.

		// UCBを計算する際の係数のうちの1つ. AlphaZeroで用いている式のC_initにあたる.
		static constexpr float UCB_FACTOR_INIT = 0.35f;

		// UCBを計算する際の係数のうちの1つ. AlphaZeroで用いている式のC_baseにあたる.
		static constexpr uint32_t UCB_FACTOR_BASE = 19652;

		// 複数スレッドで探索する際に, 特定のノードに探索が集中しないようにするために探索中のノードに与える一時的なペナルティ.
		static constexpr int32_t VIRTUAL_LOSS = 3;

		static constexpr Array<float, 3> GAME_RESULT_TO_REWARD = { 1.0f, 0.0f, 0.5f };

		evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> value_func;

		MutexPool mutex_pool;
		NodeGarbageCollector node_gc;

		reversi::Position root_state;
		std::unique_ptr<Node> root;
		EdgeLabel root_edge_label;

		// スレッドごとのノード数. 未訪問ノードに初めて訪問したときに加算される. ただし, 前回の探索から引き継いだノードは計算に入れていない.
		std::vector<uint32_t> _node_count_per_thread;

		// 探索を開始してから実行されたプレイアウト数. この値は　選択->展開->評価->バックアップ の流れを1回終えたら加算される.
		std::atomic<uint32_t> playout_count;

		// プレイアウト数の最大値.
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
		* @detail 探索ワーカー. 探索スレッド数だけ並列にこの関数が実行される.
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
		* @brief 親ノードと選択された辺に報酬を付与しつつ, virtual lossを取り除く.
		**/
		void update_statistic(Node* node, Edge& edge, double reward)	// ほかの個所では報酬はfloat型だが, 加算時にdoubleにする.
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
		* @brief パスノード用のupdate_statistic関数. virtual lossを取り除く処理が省かれている.
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
		* @brief Principal Variation(最善応手列)を取得する.
		* @detail UCTにおいては, 訪問回数が多いノードは有望なノードなので, 基本的には訪問回数の多いノードをリーフノードに至るまで選び続ける.
		* ノードの選び方の詳細:
		* 1. 訪問回数が最も多いノードを選ぶ. ただし, それが2つ以上あった場合は, 価値が最も高いノードを選ぶ.
		* 2. 勝利確定ノードがあれば訪問回数に関わらず, そのノードを選ぶ. 
		* 3. 訪問回数に関わらず, 敗北確定ノードは選ばない. ただし, 敗北確定ノードしかない場合は選ぶしかない.
		* 4. 引き分け確定ノードと敗北確定ノードしかない場合は, 引き分け確定ノードを選ぶ.
		**/
		void get_pv(Node* root, std::vector<reversi::BoardCoordinate>& pv);
	};

	template float UCT::visit_node<true>(int32_t, GameInfo&, Node*, Edge&);
	template float UCT::visit_node<false>(int32_t, GameInfo&, Node*, Edge&);
}