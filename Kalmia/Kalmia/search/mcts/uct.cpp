#include "uct.h"

#include <iomanip>
#include <algorithm>
#include <cstring>

#include "../../utils/array.h"
#include "../../utils/math_functions.h"
#include "../../utils/exception.h"

//#define SINGLE_THREAD

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace reversi;
using namespace evaluation;

namespace search::mcts
{
	bool MoveEvaluation::prior_to(const MoveEvaluation& move_eval) const
	{
		int32_t diff = this->playout_count - move_eval.playout_count;
		if (diff != 0)
			return diff > 0;
		return this->expected_reward > move_eval.expected_reward;
	}

	const SearchInfo& UCT::get_search_info()
	{
		auto& si = this->_search_info;

		auto& root_eval = si.root_eval;
		auto& child_evals = si.child_evals = DynamicArray<MoveEvaluation>(this->root->child_node_num);
		GameInfo game_info(this->root_state, PositionFeature(this->root_state));

		root_eval.effort = 1.0;
		root_eval.playout_count = this->root->visit_count;
		root_eval.game_result = edge_label_to_game_result(this->root_edge_label);
		if (root_eval.game_result == GameResult::NOT_OVER)
			root_eval.expected_reward = this->root->expected_reward();
		else
			root_eval.expected_reward = GAME_RESULT_TO_REWARD[static_cast<int32_t>(root_eval.game_result)];

		for (auto i = 0; i < child_evals.length(); i++)
		{
			MoveEvaluation& child_eval = child_evals[i];
			auto& edge = this->root->edges[i];
			child_eval.move = edge.move.coord;
			child_eval.effort = static_cast<double>(edge.visit_count) / this->root->visit_count;
			child_eval.playout_count = edge.visit_count;
			child_eval.game_result = edge_label_to_game_result(edge.label);

			if (child_eval.game_result == GameResult::NOT_OVER)
				child_eval.expected_reward = edge.expected_reward();
			else
				child_eval.expected_reward = GAME_RESULT_TO_REWARD[static_cast<int32_t>(child_eval.game_result)];

			child_eval.pv.emplace_back(child_eval.move);
			get_pv(this->root->child_nodes[i].get(), child_eval.pv);
		}
		sort(child_evals.begin(), child_evals.end(), [](const auto& e0, const auto& e1) { return e0.prior_to(e1); });

		return this->_search_info;
	}

	void UCT::set_root_state(const Position& pos)
	{
		this->root_state = pos;
		this->node_gc.add(move(this->root));
		this->root.reset(new Node());
		init_root_child_nodes();
		this->_node_count = 0;
	}

	bool UCT::transition_root_state_to_child_state(BoardCoordinate move)
	{
		if (!this->root || !this->root->is_expanded())
			return false;

		auto edges = this->root->edges.get();
		for (auto i = 0; i < this->root->child_node_num; i++)
			if (move == edges[i].move.coord && this->root->child_nodes && this->root->child_nodes[i])
			{
				unique_ptr<Node> prev_root = std::move(this->root);
				this->root = std::move(prev_root->child_nodes[i]);
				this->root_state.update<false>(move);
				init_root_child_nodes();
				this->node_gc.add(std::move(prev_root));
				this->_node_count = 0;
				return true;
			}

		return false;
	}

#ifndef SINGLE_THREAD

	SearchEndStatus UCT::search(uint32_t playout_num, int32_t time_limit_cs)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");

		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// 勝敗が判明しているのであれば探索不要.
			return SearchEndStatus::PROVED;

		this->_is_searching = true;
		this->stop_search_signal_was_sent = false;
		vector<future<void>> search_threads;

		auto thread_num = this->options.thread_num;
		auto playout_num_per_thread = playout_num / thread_num;

		milliseconds time_limit_ms(time_limit_cs * 10);
		this->search_start_time = high_resolution_clock::now();

		auto stop_flag = false;
		for (int32_t i = 0; i < thread_num; i++)
			search_threads.emplace_back(
				async(
					[&, this]
					{
						GameInfo game_info(root_state, PositionFeature(root_state));
					search_kernel(game_info, playout_num_per_thread, stop_flag);
					}));

		auto end_status = SearchEndStatus::COMPLETE;
		while (true)
		{
			if (can_stop_search(time_limit_ms, end_status))
				stop_flag = true;

			auto done = true;
			for (auto& future : search_threads)
				if (!(done &= (future.wait_for(milliseconds::zero()) == future_status::ready)))
					break;
			if (done)
				break;

			this_thread::sleep_for(milliseconds(10));
		}

		GameInfo game_info(root_state, PositionFeature(root_state));
		search_kernel(game_info, playout_num % thread_num, stop_flag);	// 余りはシングルスレッドで片づける. 高々 1以上(thread_num - 1)以下のプレイアウト数なので.

		this->_is_searching = false;
		this->search_end_time = high_resolution_clock::now();

		return end_status;
	}

#else

	// マルチスレッドだとエラーの発生場所の特定が難しいので, デバッグ用にシングルスレッドで探索する関数を用意している.
	SearchEndStatus UCT::search(uint32_t playout_num, int32_t time_limit_cs)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");


		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// 勝敗が判明しているのであれば探索不要.
			return SearchEndStatus::PROVED;

		if (!this->_is_searching)
			this->_is_searching = true;
		else
			throw invalid_operation("Cannnot run multiple search.");

		this->search_stop_flag = false;
		this->pps_counter = 0;
		auto time_limit_ms = time_limit_cs * 10;
		this->search_start_time = high_resolution_clock::now();

		future<SearchEndStatus> end_status =
			async([&, this]()
				  {
					  SearchEndStatus end_status;
					  while (_is_searching)
					  {
						  if (can_stop_search(time_limit_ms, end_status))
							  return end_status;
						  this_thread::sleep_for(milliseconds(10));
					  }
					  return SearchEndStatus::COMPLETE;
				  });

		GameInfo game_info(root_state, PositionFeature(root_state));
		search_kernel(game_info, playout_num);

		this->_is_searching = false;
		end_status.wait();
		this->search_end_time = high_resolution_clock::now();

		return end_status.get();
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
		for (auto i = 0; i < this->root->child_node_num; i++)
		{
			if (edges[i].visit_count == 0)
				this->root_state.calc_flipped_discs(edges[i].move);

			// ゲームの勝敗が確定しているかどうか調べる.
			auto pos = this->root_state;
			pos.update<false>(edges[i].move);
			if (pos.is_gameover())
			{
				auto res = to_opponent_game_result(pos.get_game_result());
				auto label = edges[i].label = game_result_to_edge_label(res);
				if (label == EdgeLabel::WIN)	// 勝利確定の辺が1つでもあれば, ルートは勝利確定.
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
			this->root_edge_label = (draw_count != 0) ? EdgeLabel::DRAW : EdgeLabel::LOSS;
	}

	void UCT::search_kernel(GameInfo& game_info, uint32_t playout_num, bool& stop_flag)
	{
		for (uint32_t i = 0; i < playout_num && !stop_flag; i++)
		{
			// ルートのゲームの情報をコピー. MCTSでは, 末端ノードに達したら一気にルートに戻るので, undoするよりもコピーのほうが速い.
			GameInfo gi = game_info;	
			visit_root_node(gi);
		}
	}

	void UCT::visit_root_node(GameInfo& game_info)
	{
		auto edges = this->root->edges.get();
		auto& node_mutex = this->mutex_pool.get(game_info.position());

		node_mutex.lock();	// 他のスレッドがrootの情報を同時に書き換えないようにするためにロックする.

		auto child_idx = select_root_child_node();
		auto& edge_to_child = edges[child_idx];
		auto first_visit = !edge_to_child.visit_count.load();	
		add_virtual_loss(this->root.get(), edge_to_child);

		node_mutex.unlock();

		if (first_visit)
		{
			this->_node_count++;
			game_info.position().calc_flipped_discs(edge_to_child.move);
			game_info.update(edge_to_child.move);
			update_statistic(this->root.get(), edge_to_child, predict_reward(game_info));
		}
		else
		{
			game_info.update(edge_to_child.move);
			update_statistic(this->root.get(), edge_to_child, visit_node<false>(game_info, this->root->child_nodes[child_idx].get(), edge_to_child));
		}
	}

	template<bool AFTER_PASS>
	double UCT::visit_node(GameInfo& game_info, Node* current_node, Edge& edge_to_current_node)
	{
		auto& node_mutex = this->mutex_pool.get(game_info.position());

		node_mutex.lock();	// 他のスレッドがcurrent_nodeの情報を同時に書き換えないようにするためにロックする.

		if (!current_node->is_expanded())
			current_node->expand(game_info.position());

		auto edges = current_node->edges.get();
		float reward;
		if (edges[0].move.coord == BoardCoordinate::PASS)
		{
			if constexpr (AFTER_PASS)	// パスが2回続いた -> 終局.
			{
				if(edges[0].visit_count == 0)
					this->_node_count++;
				auto res = game_info.position().get_game_result();
				edges[0].label = game_result_to_edge_label(res);
				edge_to_current_node.label = game_result_to_edge_label(to_opponent_game_result(res));

				node_mutex.unlock();

				reward = GAME_RESULT_TO_REWARD[edge_label_to_game_result(edges[0].label)];
			}
			else if (edges[0].is_proved())
			{
				node_mutex.unlock();

				edge_to_current_node.label = to_opponent_edge_label(edges[0].label);
				reward = GAME_RESULT_TO_REWARD[edge_label_to_game_result(edges[0].label)];
			}
			else
			{
				// パスノードは評価せずに, さらに1手先を読んで評価.
				if (!current_node->child_nodes)
				{
					current_node->init_child_nodes();
					current_node->create_child_node(0);
				}

				node_mutex.unlock();

				game_info.pass();
				reward = visit_node<true>(game_info, current_node->child_nodes[0].get(), edges[0]);
			}

			update_pass_node_statistic(current_node, edges[0], reward);
			return 1.0 - reward;
		}

		auto child_idx = select_child_node(current_node, edge_to_current_node);
		auto& edge_to_child = edges[child_idx];
		bool first_visit = !edge_to_child.visit_count.load();
		add_virtual_loss(current_node, edge_to_child);

		if (edge_to_child.is_proved())	// 勝敗が確定している辺である.
		{
			node_mutex.unlock();

			reward = GAME_RESULT_TO_REWARD[edge_label_to_game_result(edge_to_child.label)];
		}
		else if (first_visit)	// 末端の辺である.
		{
			node_mutex.unlock();

			this->_node_count++;
			game_info.position().calc_flipped_discs(edge_to_child.move);
			game_info.update(edge_to_child.move);
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

			game_info.update(edge_to_child.move);
			reward = visit_node<false>(game_info, child_node, edge_to_child);
		}

		update_statistic(current_node, edge_to_child, reward);	// ノードと辺の探索情報を更新.
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
		auto c = C_INIT + utils::log((1.0f + sum + C_BASE) / C_BASE);	// 探索の度合いに応じて, UCB式のバイアス項を調節(AlphaZeroと同様の手法).
		auto default_u = (sum == 0) ? 0.0f : sqrtf(log_sum);

		auto draw_count = 0;
		auto loss_count = 0;
		for (auto i = 0; i < this->root->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				this->root_edge_label = EdgeLabel::WIN;
				return i;	// 勝利確定ならそれを選んで終わり.
			}
			
			if (edge.is_loss())
			{
				loss_count++;
				continue;	// 敗北確定なら選ばない.
			}

			if (edge.is_draw())
				draw_count++;

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

		if (loss_count + draw_count == this->root->child_node_num)
		{
			this->root_edge_label = (draw_count != 0) ? EdgeLabel::DRAW : EdgeLabel::LOSS;

			if (draw_count != 0)
				assert(edges[max_idx].label == EdgeLabel::DRAW);
		}

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

		auto draw_count = 0;
		auto loss_count = 0;
		for (auto i = 0; i < parent->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				// 親ノードからみて勝利確定の辺があれば, 親ノードの親からみれば敗北確定.
				edge_to_parent.label = EdgeLabel::LOSS;	
				return i;
			}
			
			if (edge.is_loss())
			{
				loss_count++;
				continue;	// 敗北確定の辺は選ばない.
			}
			
			if (edge.is_draw())
				draw_count++;	

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

		if (loss_count + draw_count == parent->child_node_num)
		{
			// 親ノードからみて引き分け確定の辺があれば, 親ノードの親からみても引き分け確定.
			// 親ノードからみて敗北確定の辺があれば, 親ノードの親からみれば勝利確定.
			edge_to_parent.label = (draw_count != 0) ? EdgeLabel::DRAW : EdgeLabel::WIN;
			if (draw_count != 0)
				assert(edges[max_idx].label == EdgeLabel::DRAW);
		}

		return max_idx;
	}

	int32_t select_max_visit_count_child_node(Node* parent)
	{
		auto edges = parent->edges.get();
		uint32_t max_visit_count = 0;
		auto max_idx = 0;

		for (auto i = 0; i < parent->child_node_num; i++)
		{
			auto& edge = edges[i];

			if (edge.is_win())
				return i;

			if (edge.is_loss())
				continue;

			if (edge.visit_count >= max_visit_count)
			{
				max_visit_count = edge.visit_count;
				max_idx = i;
			}
		}
		return max_idx;
	}

	void UCT::get_pv(Node* root, std::vector<reversi::BoardCoordinate>& pv)
	{
		if (!root || !root->is_expanded())
			return;

		auto idx = select_max_visit_count_child_node(root);
		pv.emplace_back(root->edges[idx].move.coord);

		auto child_node = root->child_nodes ? root->child_nodes[idx].get() : nullptr;
		if (child_node && child_node->is_expanded())
			get_pv(child_node, pv);
		else if (pv.size() >= 2 && pv[pv.size() - 1] == BoardCoordinate::PASS && pv[pv.size() - 2] == BoardCoordinate::PASS)
		{
			// pvの末尾にパスが2連続で存在するときは終局なので取り除く.
			pv.pop_back();
			pv.pop_back();
		}
	}

	bool UCT::can_stop_search(milliseconds time_limit_ms, SearchEndStatus& end_status)
	{
		if (this->stop_search_signal_was_sent) 
		{
			end_status = SearchEndStatus::SUSPENDED_BY_STOP_SIGNAL;
			return true;
		}

		if (this->root_edge_label & EdgeLabel::PROVED)
		{
			end_status = SearchEndStatus::PROVED;
			return true;
		}

		if (search_ellapsed_ms() >= time_limit_ms)
		{
			end_status = SearchEndStatus::TIMEOUT;
			return true;
		}

		if (Node::object_count() >= this->options.node_num_limit)
		{
			end_status = SearchEndStatus::OVER_NODES;
			return true;
		}

		if (this->_early_stopping_is_enabled && can_do_early_stopping(time_limit_ms))
		{
			end_status = SearchEndStatus::EARLY_STOPPING;
			return true;
		}

		return false;
	}

	bool UCT::can_do_early_stopping(milliseconds time_limit_ms)
	{
		if (search_ellapsed_ms() < time_limit_ms * 0.1)	// 最低でも制限時間の10%は探索に費やす.
			return false;

		auto edges = this->root->edges.get();
		uint32_t first_po_count = 0;
		uint32_t second_po_count = 0;
		for (auto i = 0; i < this->root->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.visit_count > first_po_count)
			{
				second_po_count = first_po_count;
				first_po_count = edge.visit_count;
			}
			else if (edge.visit_count > second_po_count)
				second_po_count = edge.visit_count;
		}

		// 仮に残りのプレイアウトを全て次善手に費やしたとしても, 最善手のプレイアウト回数を超えることがない場合, これ以上の探索は無意味.
		return (first_po_count - second_po_count) > nps() * (time_limit_ms.count() - search_ellapsed_ms().count()) * 1.0e-3;
	}
}