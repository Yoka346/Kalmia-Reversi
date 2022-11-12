#include "node.h"

namespace search::mcts
{
	bool Edge::prior_to(const Edge& edge) const
	{
		int32_t diff = this->visit_count - edge.visit_count;
		if (diff != 0)
			return diff > 0;
		return this->expected_reward() > edge.expected_reward();
	}

	const Edge& Edge::operator=(const Edge& right)
	{
		this->label = right.label;
		this->move = right.move;
		this->visit_count = right.visit_count.load();
		this->reward_sum = right.reward_sum.load();
		return *this;
	}

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