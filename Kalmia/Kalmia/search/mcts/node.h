#pragma once

#include <memory>
#include <atomic>

#include "../../utils/array.h"
#include "../../reversi/constant.h"
#include "../../reversi/types.h"
#include "../../reversi/move.h"
#include "../../reversi/position.h"

namespace search::mcts
{
	enum EdgeLabel : uint8_t
	{
		NOT_PROVED = 0x00,	// 勝敗が確定していない.
		PROVED = 0xf0,	// 勝敗が確定している.
		WIN = PROVED | static_cast<uint8_t>(reversi::GameResult::WIN),
		LOSS = PROVED | static_cast<uint8_t>(reversi::GameResult::LOSS),
		DRAW = PROVED | static_cast<uint8_t>(reversi::GameResult::DRAW)
	};

	inline reversi::GameResult edge_label_to_game_result(EdgeLabel label)
	{
		return (label & EdgeLabel::PROVED) ? static_cast<reversi::GameResult>(label ^ EdgeLabel::PROVED) : reversi::GameResult::NOT_OVER;
	}

	inline EdgeLabel game_result_to_edge_label(reversi::GameResult result)
	{
		return static_cast<EdgeLabel>(EdgeLabel::PROVED | static_cast<uint8_t>(result));
	}

	inline EdgeLabel to_opponent_edge_label(EdgeLabel label)
	{
		return game_result_to_edge_label(to_opponent_game_result(edge_label_to_game_result(label)));
	}

	/**
	* @class
	* @brief 子ノードへ至る辺.
	* @detail ノードはヒープ上に不連続に配置されるので, UCBの計算などで直接アクセスするのは高コスト.
	* 故にUCBの計算などで必要な子ノードの情報はある程度このEdgeにキャッシュしておく.
	**/
	struct Edge
	{
		// この辺の先にある子ノードに至るための着手.
		reversi::Move move;

		std::atomic<uint32_t> visit_count;

		// 報酬の総和(ここでいう報酬とは価値関数の出力).
		std::atomic<double> reward_sum;
		EdgeLabel label;

		Edge() : move(), visit_count(0), reward_sum(0.0), label(EdgeLabel::NOT_PROVED) { ; }
		Edge(const reversi::Move& move) : move(move), visit_count(0), reward_sum(0.0), label(EdgeLabel::NOT_PROVED) { ; }
		Edge(const Edge& edge)
			: move(edge.move), visit_count(edge.visit_count.load()), 
			reward_sum(edge.reward_sum.load()), label(edge.label){ }

		double expected_reward() const { return this->reward_sum / this->visit_count; }
		bool is_proved() { return this->label & EdgeLabel::PROVED; }
		bool is_win() { return this->label == EdgeLabel::WIN; }
		bool is_loss() { return this->label == EdgeLabel::LOSS; }
		bool is_draw() { return this->label == EdgeLabel::DRAW; }
		bool prior_to(const Edge& edge) const;
		const Edge& operator=(const Edge& right);
	};

	struct Node
	{
		std::atomic<uint32_t> visit_count;

		// 他の箇所では, デバッグ効率を向上させるために, DynamicArray<T>を動的配列に用いていたが,
		// ここではNodeオブジェクトのサイズをできる限り減らしたいので使わない.

		// 子ノードに至る辺
		std::unique_ptr<Edge[]> edges;

		std::unique_ptr<std::unique_ptr<Node>[]> child_nodes;
		uint8_t child_node_num;

		Node() : visit_count(0), edges(nullptr), child_nodes(nullptr), child_node_num(0) { _object_count++; }
		~Node() { _object_count--; }

		static uint64_t object_count() { return _object_count; }

		double expected_reward();
		bool is_expanded() { return this->edges.get(); }
		Node* create_child_node(int32_t idx) { return (this->child_nodes[idx] = std::make_unique<Node>()).get(); }
		void init_child_nodes() { this->child_nodes = std::make_unique<std::unique_ptr<Node>[]>(this->child_node_num); }

		/**
		* @fn
		* @brief 候補手の数だけ辺を伸ばす.
		**/
		void expand(const reversi::Position& pos)
		{
			Array<reversi::Move, reversi::MAX_MOVE_NUM> moves;
			if (!(this->child_node_num = pos.get_next_moves(moves)))
			{
				this->child_node_num = 1;
				this->edges = std::make_unique<Edge[]>(1);
				this->edges[0].move.coord = reversi::BoardCoordinate::PASS;
				return;
			}

			this->edges = std::make_unique<Edge[]>(this->child_node_num);
			for (int32_t i = 0; i < this->child_node_num; i++)
				this->edges[i].move = moves[i];
		}

	private:
		inline static std::atomic<uint64_t> _object_count = 0ULL;	// 現存するNodeオブジェクトの個数. 
	};
}