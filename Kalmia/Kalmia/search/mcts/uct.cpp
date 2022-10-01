#include "uct.h"

#include "../../utils/math_functions.h"
#include "../../utils/exception.h"

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace reversi;
using namespace evaluation;

namespace search::mcts
{
	void UCT::set_root_state(const Position& pos)
	{
		this->root_state = pos;
		this->root.reset(new Node());
		init_root_child_nodes();
	}

	bool UCT::transition_root_state_to_child_state(BoardCoordinate move)
	{
		if (this->root && this->root->edges)
		{
			auto edges = this->root->edges.get();
			for (int32_t i = 0; i < this->root->child_node_num; i++)
				if (move == edges[i].move.coord && this->root->child_nodes && this->root->child_nodes[i])
				{
					unique_ptr<Node> prev_root = std::move(this->root);
					this->root = std::move(prev_root->child_nodes[i]);
					this->root_state.update<false>(move);
					init_root_child_nodes();
					this->node_gc.add(std::move(prev_root));
					return true;
				}
		}
		return false;
	}

	void UCT::search(uint32_t playout_num, int32_t time_limit_ms)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");

		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// 勝敗が判明しているのであれば探索不要.
			return;

		this->_is_searching.store(true);
		this->search_stop_flag.store(false);
		this->pps_counter = 0;
		vector<thread> search_threads;
		this->search_start_time = high_resolution_clock::now();

		auto thread_num = this->OPTIONS.thread_num;
		auto playout_num_per_thread = playout_num / thread_num;	// プレイアウト数をスレッド数で割って, 均等に各スレッドにタスクを割り当てる. 全てのスレッドがほぼ同時にタスクを終えることを想定しているが, 
																// もしそうでなければ, スレッドの待機が発生して非効率かもしれない. ToDo: 各スレッドの処理時間が概ね均等かどうか調べる.
		for (int32_t i = 0; i < thread_num; i++)
			search_threads.emplace_back(
				thread(
				[&, this]
				{
						GameInfo game_info(root_state, PositionFeature(root_state));
						search_kernel(game_info, playout_num_per_thread, time_limit_ms);
				}));

		for (auto& thread : search_threads)	
			thread.join();

		GameInfo game_info(root_state, PositionFeature(root_state));
		search_kernel(game_info, playout_num % thread_num, time_limit_ms);	// 余りはシングルスレッドで片づける. 高々 1以上(thread_num - 1)以下のプレイアウト数なので.

		this->search_end_time = high_resolution_clock::now();
		this->_is_searching.store(false);
	}

#ifdef _DEBUG

	// マルチスレッドだとエラーの発生場所の特定が難しいので, デバッグ用にシングルスレッドで探索する関数を用意している.
	void UCT::search_on_single_thread(uint32_t playout_num, int32_t time_limit_ms)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");

		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// 勝敗が判明しているのであれば探索不要.
			return;

		this->search_stop_flag.store(false);
		this->pps_counter = 0;
		this->_is_searching.store(true);
		this->search_start_time = high_resolution_clock::now();

		GameInfo game_info(root_state, PositionFeature(root_state));
		search_kernel(game_info, playout_num, time_limit_ms);	

		this->search_end_time = high_resolution_clock::now();
		this->_is_searching.store(false);
	}

#endif

	void UCT::init_root_child_nodes()
	{
		// ルート直下の子ノードは, プレイアウト数が極端に少なく無い限り必ず訪問するので, 先に展開を済ませておく.
		if (!this->root->edges)
			this->root->expand(this->root_state);

		if (!this->root->child_nodes)
			this->root->init_child_nodes();

		this->root_edge_label = EdgeLabel::NOT_PROVED;

		auto edges = this->root->edges.get();
		auto loss_count = 0;
		auto draw_count = 0;
		for (int32_t i = 0; i < this->root->child_node_num; i++)
		{
			if (!edges[i].visit_count)
				this->root_state.calc_flipped_discs(edges[i].move);

			// ゲームの勝敗が確定しているかどうか調べる.
			auto pos = this->root_state;
			pos.update<false>(edges[i].move);
			if (pos.is_gameover())
			{
				auto res = to_opponent_result(pos.get_game_result());
				auto label = edges[i].label = static_cast<EdgeLabel>(static_cast<uint8_t>(res) | EdgeLabel::PROVED);
				if (label == EdgeLabel::WIN)	// 勝利確定の辺1つでもあれば, ルートは勝利確定.
				{
					this->root_edge_label = EdgeLabel::WIN;
					return;
				}

				if (label == EdgeLabel::LOSS)
					loss_count++;
				else if (label == EdgeLabel::DRAW)
					draw_count++;
			}
			else
				edges[i].label = EdgeLabel::NOT_PROVED;

			if (!this->root->child_nodes[i])
				this->root->create_child_node(i);
		}

		if (loss_count + draw_count == this->root->child_node_num)	
			this->root_edge_label = draw_count ? EdgeLabel::DRAW : EdgeLabel::LOSS;
	}

	void UCT::search_kernel(GameInfo& game_info, uint32_t playout_num, int32_t time_limit_ms)
	{
		auto stop =
		[&, this]
		{
			return search_stop_flag.load()
				|| this->search_ellapsed_ms() >= time_limit_ms
				|| root_edge_label & EdgeLabel::PROVED
				|| Node::object_count() >= OPTIONS.node_num_limit;
		};

		for (uint32_t i = 0; i < playout_num && !stop(); i++)
		{
			auto gi = game_info;	// ルートのゲームの情報をコピー. MCTSでは, 末端ノードに達したら一気にルートに戻るので, undoするよりもコピーのほうが速い.
			visit_root_node(gi);
		}
	}

	void UCT::visit_root_node(GameInfo& game_info)
	{
		auto edges = this->root->edges.get();
		auto& node_mutex = this->mutex_pool.get(game_info.position());

		node_mutex.lock();
		auto child_idx = select_root_child_node();
		auto& edge = edges[child_idx];
		auto child_visit_count = edge.visit_count.load();	
		add_virtual_loss(this->root.get(), edge);
		node_mutex.unlock();

		game_info.update(edge.move);
		if (child_visit_count)	
			update_statistic(this->root.get(), edge, visit_node<false>(game_info, this->root->child_nodes[child_idx].get(), edge));
		else
			update_statistic(this->root.get(), edge, predict_reward(game_info));
	}

	template<bool AFTER_PASS>
	double UCT::visit_node(GameInfo& game_info, Node* current_node, Edge& edge_to_current_node)
	{
		auto& node_mutex = this->mutex_pool.get(game_info.position());

		node_mutex.lock();	// 他のスレッドがcurrent_nodeの情報を同時に書き換えないようにするためにロックする.

		if (!current_node->is_expanded())
			current_node->expand(game_info.position());

		auto child_idx = select_child_node(current_node, edge_to_current_node);
		auto& edge_to_child = this->root->edges[child_idx];
		bool first_visit = !edge_to_child.visit_count.load();
		add_virtual_loss(current_node, edge_to_child);
		game_info.update(edge_to_child.move);

		float reward;
		if (first_visit)	// 末端の辺である.
		{
			node_mutex.unlock();
			reward = predict_reward(game_info);
		}
		else	// 辺の先に子ノードがある, もしくは子ノードを作るべき辺である.
		{
			if (!current_node->child_nodes)
				current_node->init_child_nodes();

			auto child_node = current_node->child_nodes[child_idx].get();
			if (!child_node)
				child_node = current_node->create_child_node(child_idx);	

			node_mutex.unlock();

			reward = visit_node<false>(game_info, child_node, edge_to_child);
		}

		node_mutex.unlock();

		update_statistic(current_node, edge_to_current_node, reward);	// ノードと辺の探索情報を更新.
		return 1.0 - reward;
	}

	int32_t UCT::select_root_child_node()
	{
		constexpr auto C_BASE = UCB_FACTOR_BASE;
		constexpr auto C_INIT = UCB_FACTOR_INIT;

		auto edges = this->root->edges.get();
		auto max_idx = 0;
		auto max_score = -INFINITY;
		auto sum = this->root->visit_count.load();
		auto log_sum = utils::log(sum);
		auto c = C_INIT + utils::log((1.0f + sum + C_BASE) / C_BASE);	// 探索の度合いに応じて, UCB式のバイアス項を調節(Alpha Zeroと同様の手法).
		auto default_u = (sum == 0) ? 0.0f : sqrtf(log_sum);

		auto draw_idx = -1;
		for (auto i = 0; i < this->root->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				this->root_edge_label = EdgeLabel::WIN;
				return i;	// 勝利確定ならそれを選んで終わり.
			}
			
			if (edges->is_loss())
				continue;	// 敗北確定なら選ばない.

			if (edge.is_draw())
				draw_idx = i;

			auto n = edge.visit_count.load();
			float q, u;	
			if (n == 0)
			{
				q = ROOT_FPU;
				u = default_u;
			}
			else
			{
				q = static_cast<float>(edge.expected_reward());
				u = c * sqrtf(log_sum / n);
			}

			auto score = q + u;
			if (score > max_score)
			{
				max_score = score;
				max_idx = i;
			}
		}

		if(edges[max_idx].is_loss())
			if (draw_idx != -1)	
			{
				this->root_edge_label = EdgeLabel::DRAW;
				return draw_idx;
			}
			else 
				this->root_edge_label = EdgeLabel::LOSS;
		return max_idx;
	}

	int32_t UCT::select_child_node(Node* parent, Edge& edge_to_parent)
	{
		constexpr auto C_BASE = UCB_FACTOR_BASE;
		constexpr auto C_INIT = UCB_FACTOR_INIT;

		auto edges = parent->edges.get();
		auto max_idx = 0;
		auto max_score = -INFINITY;
		auto sum = parent->visit_count.load();
		auto log_sum = utils::log(sum);
		auto c = C_INIT + utils::log((1.0f + sum + C_BASE) / C_BASE);
		auto default_u = (sum == 0) ? 0.0f : sqrtf(log_sum);
		auto fpu = static_cast<float>(edge_to_parent.expected_reward());	// 未訪問ノードは親ノードの価値でUCBを計算する.

		auto draw_idx = -1;
		for (auto i = 0; i < parent->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				// 親ノードからみて勝ち確定の辺があれば, 親ノードの親からみれば負け確定.
				edge_to_parent.label = EdgeLabel::LOSS;	
				return i;
			}
			
			if (edge.is_loss())	// 敗北確定の辺は選ばない.
				continue;
			
			if (edge.is_draw())
				draw_idx = i;	// 引き分けの辺はとりあえず記録.

			auto n = edge.visit_count.load();
			float q, u;
			if (n == 0)
			{
				q = fpu;
				u = default_u;
			}
			else
			{
				q = static_cast<float>(edge.expected_reward());
				u = c * sqrtf(log_sum / n);
			}

			auto score = q + u;
			if (score > max_score)
			{
				max_score = score;
				max_idx = i;
			}
		}

		if (edges[max_idx].is_loss())
			if (draw_idx != -1)		
			{
				edge_to_parent.label = EdgeLabel::DRAW;
				return draw_idx;
			}
			else
				edge_to_parent.label = EdgeLabel::WIN; // 親ノードからみて全ての辺が敗北確定であれば, 親ノードの親からみれば勝利確定.
		return max_idx;
	}
}