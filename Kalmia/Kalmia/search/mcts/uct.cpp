#include "uct.h"

#include <iomanip>
#include <algorithm>

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
	const SearchInfo& UCT::get_search_info()
	{
		auto& si = this->_search_info;
		auto& root_eval = si.root_eval;
		auto& child_evals = si.child_evals = DynamicArray<MoveEvaluation>(this->root->child_node_num);
		GameInfo game_info(this->root_state, PositionFeature(this->root_state));

		root_eval.effort = 1.0;
		root_eval.playout_count = this->root->visit_count;
		root_eval.expected_reward = this->root->expected_reward();
		root_eval.game_result = edge_label_to_game_result(this->root_edge_label);

		auto edges = this->root->edges.get();
		for (auto i = 0; i < child_evals.length(); i++)
		{
			auto& child_eval = child_evals[i];
			auto& edge = edges[i];
			child_eval.effort = edge.visit_count / this->root->visit_count;
			child_eval.playout_count = edge.visit_count;
			child_eval.expected_reward = edge.expected_reward();
			child_eval.game_result = edge_label_to_game_result(edge.label);
			get_pv(this->root->child_nodes[i].get(), child_eval.pv);
		}

		return this->_search_info;
	}

	void UCT::set_root_state(const Position& pos)
	{
		this->root_state = pos;
		this->node_gc.add(move(this->root));
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

#ifndef SINGLE_THREAD
	SearchEndStatus UCT::search(uint32_t playout_num, int32_t time_limit_ms)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");

		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// ���s���������Ă���̂ł���ΒT���s�v.
			return SearchEndStatus::PROVED;

		this->_is_searching = true;
		this->search_stop_flag = false;
		this->pps_counter = 0;
		vector<thread> search_threads;

		auto thread_num = this->options.thread_num;
		auto playout_num_per_thread = playout_num / thread_num;	// �v���C�A�E�g�����X���b�h���Ŋ�����, �ϓ��Ɋe�X���b�h�Ƀ^�X�N�����蓖�Ă�. �S�ẴX���b�h���قړ����Ƀ^�X�N���I���邱�Ƃ�z�肵�Ă��邪, 
			
		this->search_start_time = high_resolution_clock::now();
		future<SearchEndStatus> end_status = exec_search_stop_condition_checker(time_limit_ms);

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

		if (!this->search_stop_flag)
		{
			GameInfo game_info(root_state, PositionFeature(root_state));
			search_kernel(game_info, playout_num % thread_num, time_limit_ms);	// �]��̓V���O���X���b�h�ŕЂÂ���. ���X 1�ȏ�(thread_num - 1)�ȉ��̃v���C�A�E�g���Ȃ̂�.
		}
		
		this->_is_searching = false;
		end_status.wait();
		this->search_end_time = high_resolution_clock::now();

		return end_status.get();
	}

#else

	// �}���`�X���b�h���ƃG���[�̔����ꏊ�̓��肪����̂�, �f�o�b�O�p�ɃV���O���X���b�h�ŒT������֐���p�ӂ��Ă���.
	SearchEndStatus UCT::search(uint32_t playout_num, int32_t time_limit_ms)
	{
		if (!this->root)
			throw invalid_operation("Set root state before search.");

		auto child_idx = select_root_child_node();
		if (this->root->edges[child_idx].is_proved())	// ���s���������Ă���̂ł���ΒT���s�v.
			return;

		this->_is_searching = true;
		this->search_stop_flag = false;
		this->pps_counter = 0;
		this->search_start_time = high_resolution_clock::now();
		future<SearchEndStatus> res = exec_search_stop_condition_checker(time_limit_ms);

		GameInfo game_info(root_state, PositionFeature(root_state));
		search_kernel(game_info, playout_num, time_limit_ms);

		this->_is_searching = false;
		res.wait();
		this->search_end_time = high_resolution_clock::now();

		return res.get();
	}

#endif

	void UCT::init_root_child_nodes()
	{
		// ���[�g�����̎q�m�[�h��, �v���C�A�E�g�����ɒ[�ɏ��Ȃ���������K���K�₷��̂�, ��ɓW�J���ς܂��Ă���.
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

			// �Q�[���̏��s���m�肵�Ă��邩�ǂ������ׂ�.
			auto pos = this->root_state;
			pos.update<false>(edges[i].move);
			if (pos.is_gameover())
			{
				auto res = to_opponent_result(pos.get_game_result());
				auto label = edges[i].label = static_cast<EdgeLabel>(static_cast<uint8_t>(res) | EdgeLabel::PROVED);
				if (label == EdgeLabel::WIN)	// �����m��̕�1�ł������, ���[�g�͏����m��.
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
		for (uint32_t i = 0; i < playout_num && !this->search_stop_flag; i++)
		{
			auto gi = game_info;	// ���[�g�̃Q�[���̏����R�s�[. MCTS�ł�, ���[�m�[�h�ɒB�������C�Ƀ��[�g�ɖ߂�̂�, undo��������R�s�[�̂ق�������.
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

		node_mutex.lock();	// ���̃X���b�h��current_node�̏��𓯎��ɏ��������Ȃ��悤�ɂ��邽�߂Ƀ��b�N����.

		if (!current_node->is_expanded())
			current_node->expand(game_info.position());

		float reward;
		if (current_node->edges[0].move.coord == BoardCoordinate::PASS)
		{
			if constexpr (AFTER_PASS)	// �p�X��2�񑱂��� -> �I��.
			{
				auto res = game_info.position().get_game_result();
				current_node->edges[0].label = game_result_to_edge_label(res);
				edge_to_current_node.label = game_result_to_edge_label(to_opponent_result(res));
				node_mutex.unlock();
				reward = EDGE_LABEL_TO_REWARD[current_node->edges[0].label];
			}
			else if (current_node->edges[0].is_proved())
			{
				node_mutex.unlock();
				edge_to_current_node.label = to_opponent_edge_label(current_node->edges[0].label);
				reward = EDGE_LABEL_TO_REWARD[current_node->edges[0].label];
			}
			else
			{
				// �p�X�m�[�h�͕]��������, �����1����ǂ�ŕ]��.
				if (!current_node->child_nodes)
				{
					current_node->init_child_nodes();
					current_node->create_child_node(0);
				}
				node_mutex.unlock();

				game_info.pass();
				reward = visit_node<true>(game_info, current_node->child_nodes[0].get(), current_node->edges[0]);
			}
		}
		else
		{
			auto child_idx = select_child_node(current_node, edge_to_current_node);
			auto& edge_to_child = this->root->edges[child_idx];
			bool first_visit = !edge_to_child.visit_count.load();
			add_virtual_loss(current_node, edge_to_child);
			game_info.update(edge_to_child.move);

			if (edge_to_child.is_proved())	// ���s���m�肵�Ă���ӂł���.
			{
				node_mutex.unlock();
				reward = EDGE_LABEL_TO_REWARD[edge_to_child.label];
			}
			else if (first_visit)	// ���[�̕ӂł���.
			{
				node_mutex.unlock();
				reward = predict_reward(game_info);
			}
			else	// �ӂ̐�Ɏq�m�[�h������, �������͎q�m�[�h�����ׂ��ӂł���.
			{
				if (!current_node->child_nodes)
					current_node->init_child_nodes();

				auto child_node = current_node->child_nodes[child_idx].get();
				if (!child_node)
					child_node = current_node->create_child_node(child_idx);

				node_mutex.unlock();

				reward = visit_node<false>(game_info, child_node, edge_to_child);
			}
		}

		update_statistic(current_node, edge_to_current_node, reward);	// �m�[�h�ƕӂ̒T�������X�V.
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
		auto c = C_INIT + utils::log((1.0f + sum + C_BASE) / C_BASE);	// �T���̓x�����ɉ�����, UCB���̃o�C�A�X���𒲐�(Alpha Zero�Ɠ��l�̎�@).
		auto default_u = (sum == 0) ? 0.0f : sqrtf(log_sum);

		auto draw_idx = -1;
		for (auto i = 0; i < this->root->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				this->root_edge_label = EdgeLabel::WIN;
				return i;	// �����m��Ȃ炻���I��ŏI���.
			}
			
			if (edges->is_loss())
				continue;	// �s�k�m��Ȃ�I�΂Ȃ�.

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
		auto fpu = static_cast<float>(edge_to_parent.expected_reward());	// ���K��m�[�h�͐e�m�[�h�̉��l��UCB���v�Z����.

		auto draw_idx = -1;
		for (auto i = 0; i < parent->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
			{
				// �e�m�[�h����݂ď����m��̕ӂ������, �e�m�[�h�̐e����݂�Ε����m��.
				edge_to_parent.label = EdgeLabel::LOSS;	
				return i;
			}
			
			if (edge.is_loss())	// �s�k�m��̕ӂ͑I�΂Ȃ�.
				continue;
			
			if (edge.is_draw())
				draw_idx = i;	// ���������̕ӂ͂Ƃ肠�����L�^.

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
				edge_to_parent.label = EdgeLabel::WIN; // �e�m�[�h����݂đS�Ă̕ӂ��s�k�m��ł����, �e�m�[�h�̐e����݂�Ώ����m��.
		return max_idx;
	}

	int32_t select_max_visit_count_child_node(Node* parent)
	{
		auto edges = parent->edges.get();
		uint32_t max_visit = 0;
		auto max_idx = 0;
		auto draw_idx = -1;

		for (auto i = 0; i < parent->child_node_num; i++)
		{
			auto& edge = edges[i];
			if (edge.is_win())
				return i;

			if (edge.is_loss())
				continue;

			if (edge.is_draw())
				draw_idx = i;	

			if (edge.visit_count > max_visit)
			{
				max_visit = edge.visit_count;
				max_idx = i;
			}
		}
		return (edges[max_idx].is_loss() && draw_idx != -1) ? draw_idx : max_idx;
	}

	void UCT::get_pv(Node* root, std::vector<reversi::BoardCoordinate>& pv)
	{
		if (!root || !root->is_expanded())
			return;

		auto idx = select_max_visit_count_child_node(root);
		pv.emplace_back(root->edges[idx].move.coord);
		
		if (!root->child_nodes)
			get_pv(root->child_nodes[idx].get(), pv);
	}

	future<SearchEndStatus> UCT::exec_search_stop_condition_checker(int32_t time_limit_ms)
	{
		return async([&, this]()
			  {
				  while (_is_searching)
				  {
					  if (root_edge_label & EdgeLabel::PROVED)
					  {
						  send_stop_search_signal();
						  return SearchEndStatus::PROVED;
					  }

					  if (search_stop_flag)
						  return SearchEndStatus::SUSPENDED_BY_STOP_SIGNAL;

					  if (search_ellapsed_ms() >= time_limit_ms)
					  {
						  send_stop_search_signal();
						  return SearchEndStatus::TIMEOUT;
					  }

					  if (Node::object_count() >= options.node_num_limit)
					  {
						  send_stop_search_signal();
						  return SearchEndStatus::SUSPENDED_DUE_TO_OVER_NODES;
					  }

					  if (can_do_early_stopping(time_limit_ms))
					  {
						  send_stop_search_signal();
						  return SearchEndStatus::EARLY_STOPPING;
					  }

					  this_thread::sleep_for(milliseconds(10));
				  }
				  return SearchEndStatus::COMPLETE;
			  });
	}

	bool UCT::can_do_early_stopping(int32_t time_limit_ms)
	{
		if (search_ellapsed_ms() < time_limit_ms * 0.1)	// �Œ�ł��������Ԃ�10%�͒T���ɔ�₷.
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

		// ���Ɏc��̃v���C�A�E�g��S�Ď��P��ɔ�₵���Ƃ��Ă�, �őP��̃v���C�A�E�g�񐔂𒴂��邱�Ƃ��Ȃ��ꍇ, ����ȏ�̒T���͖��Ӗ�.
		return (first_po_count - second_po_count) > pps() * (time_limit_ms - search_ellapsed_ms()) * 1.0e-3;
	}
}