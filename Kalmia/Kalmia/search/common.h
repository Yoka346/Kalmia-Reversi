#pragma once

#include "../reversi/move.h"
#include "../reversi/position.h"
#include "../evaluate/feature.h"

namespace search
{
	/**
	* @class
	* @brief 盤面とその特徴をまとめて管理するクラス.
	**/
	class GameInfo
	{
	public:
		GameInfo(const reversi::Position& pos, const evaluation::PositionFeature& feature) :_position(pos), _feature(feature) { ; }

		const reversi::Position& position() { return this->_position; }
		const evaluation::PositionFeature& feature() { return this->_feature; }

		void update(const reversi::Move& move) { this->_position.update<false>(move); this->_feature.update(move); }
		void pass() { this->_position.pass(); this->_feature.pass(); }

	private:
		reversi::Position _position;
		evaluation::PositionFeature _feature;
	};
}
