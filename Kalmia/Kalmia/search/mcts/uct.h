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
		// 探索スレッド数(デフォルトはCPUの論理コア数).
		int32_t thread_num = std::thread::hardware_concurrency();

		// メモリ上のNodeオブジェクトの数(Node::object_count())の上限.
		uint64_t node_num_limit = static_cast<uint64_t>(2e+7);

		// 価値関数のパラメータファイルの場所.
		std::string value_func_param_file_path;
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
	* @Nodeオブジェクトをロックするためのmutexを提供するクラス.
	**/
	class MutexPool
	{
	private:
		static constexpr size_t SIZE = 1 << 16;	// 余剰の計算が楽になるのでサイズは2^nにする.

		Array<std::mutex, SIZE> pool;

	public:
		/**
		* @fn
		* @brief 盤面をキーとして, プールからmutexを取得する.
		* @detail 極々稀にキーが衝突するが, 無駄なロックが発生するだけなので特に問題はない. むしろ衝突回避の処理を挟む方が高コスト.
		**/
		std::mutex& get(const reversi::Position& pos)
		{
			// pos.calc_hash_code() & (SIZE - 1) は pos.calc_hash_code() % SIZE と同じ意味. SIZE == 2^n だから成り立つ.
			return this->pool[pos.calc_hash_code() & (SIZE - 1)];
		}
	};

	/**
	* @class
	* @brief UCT(Upper Confidence Tree, 信頼上限木)を表すクラス.
	* @detail UCTはルートノードを持ち, そのルートノードから木が展開されている. 探索に関わる処理は全てのこのクラスのメンバ関数として実装されている.
	**/
	class UCT
	{
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

		static constexpr Array<double, 3> EDGE_LABEL_TO_REWARD = { 1.0, 0.0, 0.5 };

		const UCTOptions OPTIONS;
		const evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> VALUE_FUNC;

		MutexPool mutex_pool;
		NodeGarbageCollector node_gc;

		reversi::Position root_state;
		std::unique_ptr<Node> root;
		EdgeLabel root_edge_label;

		// pps(playout per second)を計算するためのカウンター.
		std::atomic<uint32_t> pps_counter;

		SearchInfo _search_info;
		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		std::atomic<bool> search_stop_flag = true;
		std::atomic<bool> _is_searching = false;

		void init_root_child_nodes();

		/**
		* @fn
		* @detail 探索ワーカー. 探索スレッド数だけ並列にこの関数が実行される.
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
		* @brief 親ノードと選択された辺に報酬を付与しつつ, virtual lossを取り除く.
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
		* @brief Principal Variation(最善応手列)を取得する.
		* @detail UCTにおいては, 訪問回数が多いノードは有望なノードなので, 基本的には訪問回数の多いノードをリーフノードに至るまで選び続ける.
		* ノードの選び方の詳細:
		* 1. 訪問回数が最も多いノードを選ぶ. ただし, それが2つ以上あった場合は, 価値が最も高いノードを選ぶ.
		* 2. 勝利確定ノードがあれば訪問回数に関わらず, そのノードを選ぶ. 
		* 3. 最も訪問回数の多いノードが敗北確定ノードであれば, 次に訪問回数の多いノードを選ぶ.
		* 4. 引き分け確定ノードと敗北確定ノードしかない場合は, 引き分け確定ノードを選ぶ.
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
		* @brief ルート盤面を着手によって次の盤面に遷移させる.
		* @param (move) 着手位置.
		* @return 遷移できたらtrue.
		**/
		bool transition_root_state_to_child_state(reversi::BoardCoordinate move);

		/**
		* @fn
		* @brief 探索を行う. 制限時間はINT32_MAX[ms](約24.8日)
		* @param (playout_num) プレイアウト回数(ここでは選択->展開->評価->バックアップの流れを実行する回数).
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