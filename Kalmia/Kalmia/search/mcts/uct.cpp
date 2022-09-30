#include "uct.h"

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

		auto edges = this->root->edges.get();
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
				edges[i].label = static_cast<EdgeLabel>(static_cast<uint8_t>(res) | EdgeLabel::PROVED);
			}
			else
				edges[i].label = EdgeLabel::NOT_PROVED;

			if (!this->root->child_nodes[i])
				this->root->create_child_node(i);
		}
	}

	void UCT::search_kernel(GameInfo& game_info, uint32_t playout_num, int32_t time_limit_ms)
	{
		auto stop =
		[&, this]
		{
			return search_stop_flag.load()
				|| this->search_ellapsed_ms() >= time_limit_ms
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
			update_statistic(this->root.get(), edge, predict_value(game_info));
	}
}