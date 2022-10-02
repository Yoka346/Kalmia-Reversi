#include"engine.h"

using namespace reversi;

namespace engine
{
	bool Engine::update_position(DiscColor color, BoardCoordinate move)
	{
		if (color != this->_position.side_to_move())
			this->_position.pass();

		this->position_history.emplace_back(this->_position);

		if (move == BoardCoordinate::PASS)	
		{
			this->_position.pass();
			return true;
		}

		Move m(move, 0ULL);
		this->_position.calc_flipped_discs(m);
		if (!this->_position.update<true>(m))
		{
			this->position_history.pop_back();
			return false;
		}
		return true;
	}

	bool Engine::undo_position()
	{
		if (!this->position_history.size())
			return false;

		this->_position = this->position_history.back();	
		this->position_history.pop_back();
		return true;
	}

	void Engine::generate_move(DiscColor color, BoardCoordinate& move)
	{
		if (this->_position.side_to_move() != color)
			this->_position.pass();
		// ToDo: ‚±‚±‚©‚ç
	}
}