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
		// 探索スレッド数(デフォルトはCPUの論理コア数).
		int32_t thread_num = std::thread::hardware_concurrency();

		// メモリ上のNodeオブジェクトの数(Node::object_count())の上限.
		uint64_t node_num_limit = 2e+7;

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

		// この着手に費やされたプレイアウト(ここでは価値関数の呼び出しを指す)の回数.
		uint32_t playout_count;

		// 探索の結果得られた, この着手から先の期待勝率(厳密には価値の期待値). 
		double expected_value;

		// 価値関数の出力. すなわち探索を一切せずに予測した勝率(価値).
		double raw_value;

		// Principal Variation(最善応手列).
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

		// 複数スレッドで探索する際に, 特定のノードに探索が集中しないようにするために, 探索中のノードに与える一時的なペナルティ.
		static constexpr uint32_t VIRTUAL_LOSS = 3;

		static constexpr Array<EdgeLabel, 3> GAME_RESULT_TO_EDGE_LABEL = { EdgeLabel::LOSS, EdgeLabel::DRAW, EdgeLabel::WIN };

		const UCTOptions OPTIONS;
		const evaluation::ValueFunction<evaluation::ValueRepresentation::WIN_RATE> VALUE_FUNC;

		reversi::Position root_state;
		Node root;

		// pps(playout per second)を計算するためのカウンター.
		std::atomic<uint32_t> pps_counter;

		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;
		std::atomic<bool> search_stop_flag = true;
		bool _is_searching = false;

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
		int32_t select_child_node();

		float playout(GameInfo& game_info)
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
		* @detail UCTにおいては, 訪問回数が多いノードは有望なノードなので, 基本的には訪問回数の多いノードを木の末端に至るまで選び続ける.
		* ノードの選び方の詳細:
		* 1. 訪問回数が最も多いノードを選ぶ. ただし, それが2つ以上あった場合は, 価値が最も高いノードを選ぶ.
		* 2. 勝利確定ノードがあれば訪問回数に関わらず, そのノードを選ぶ. ただし, 勝利確定ノードが複数存在する場合は, 最終石差が最大のノードを選ぶ.
		* 3. 最も訪問回数の多いノードが敗北確定ノードであれば, 次に訪問回数の多いノードを選ぶ.
		* 4. 引き分け確定ノードと負け確定ノードしかない場合は, 引き分け確定ノードを選ぶ.
		* 5. 敗北確定ノードしか無い場合は, 最終石差が最小のノードを選ぶ.
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
		void send_stop_search_signal() { if (this->_is_searching) this->search_stop_flag.store(true); }
	};

	template double UCT::visit_node<true>(GameInfo&, Node*, Edge&);
	template double UCT::visit_node<false>(GameInfo&, Node*, Edge&);
}