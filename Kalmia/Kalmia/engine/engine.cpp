#include"engine.h"

using namespace reversi;

namespace engine
{
	bool Engine::update_position(DiscColor color, BoardCoordinate move)
	{
		if (color != this->_position.side_to_move())
		{
			this->_position.pass();
			this->move_history.emplace_back(BoardCoordinate::PASS, 0ULL);
		}

		Move m(move, 0ULL);
		this->_position.calc_flipped_discs(m);
		if (this->_position.update<true>(m))
		{
			this->move_history.emplace_back(m.coord, m.flipped);
			return true;
		}
		return false;
	}

	bool Engine::undo_position()
	{
		if (!this->move_history.size())
			return false;

		Move& move = this->move_history.back();
		this->move_history.pop_back();
		if (move.coord != BoardCoordinate::PASS)
			this->_position.undo(move);
		else
			this->_position.pass();
		return true;
	}
}