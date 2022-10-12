#include "node.h"

namespace search::mcts
{
	double Node::expected_reward()
	{
		if (!is_expanded())
			return NAN;

		auto reward = 0.0;
		auto edges = this->edges.get();
		for (auto i = 0; i < this->child_node_num; i++)
			reward += edges[i].reward_sum / this->visit_count;
		return reward;
	}
}