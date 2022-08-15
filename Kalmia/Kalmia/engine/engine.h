#pragma once
#include "../common.h"
#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/position.h"

namespace engine
{
	class Engine
	{
	private:
		std::string _name;
		std::string _version;
		utils::GameTimer timer;
		reversi::Position _position;

	public:
		Engine(std::string name, std::string version) : _name(name), _version(version), _position(), timer() { ; }
		inline const std::string& name() const { return this->_name; }
		inline const std::string& version() const { return this->_version; }
		inline const reversi::Position& position() const { return this->_position; }
		inline virtual void set_position(reversi::Position pos) { this->_position = pos; }

		inline void set_time(std::chrono::milliseconds main_time, std::chrono::milliseconds byoyomi, std::chrono::milliseconds inc)
		{
			this->timer = GameTimer(main_time, byoyomi, inc);
		}

		virtual void start_thinking();
		virtual reversi::BoardCoordinate stop_thinking();
	};
}