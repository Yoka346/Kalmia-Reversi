#include"engine.h"

using namespace std;

using namespace reversi;

namespace engine
{
	void Engine::send_text_message(const std::string& msg) { this->on_message_was_sent(msg); }
	void Engine::send_err_message(const std::string& msg) { this->on_err_message_was_sent(msg); }
	void Engine::send_think_info(ThinkInfo& think_info) { this->on_think_info_was_sent(think_info); }
	void Engine::send_multi_pv(MultiPV& multi_pv) { this->on_multi_pv_was_sent(multi_pv); }
	void Engine::send_move(EngineMove& move) { this->on_move_was_sent(move); }

	bool Engine::ready() 
	{ 
		if (!on_ready()) 
			return false; 
		this->_state = EngineState::READY; 
		return true;
	}

	void Engine::start_game() { on_start_game(); this->_state = EngineState::PLAYING; }
	void Engine::end_game() { on_end_game(); this->_state = EngineState::GAME_OVER; }

	void Engine::set_position(reversi::Position& pos) 
	{ 
		this->_position = pos; 
		this->position_history.clear(); 
		on_position_was_set(); 
	}

	void Engine::clear_position()
	{ 
		this->_position = reversi::Position(); 
		this->position_history.clear(); 
		on_cleared_position(); 
	}

	bool Engine::update_position(DiscColor color, BoardCoordinate move)
	{
		if (color != this->_position.side_to_move())
			this->_position.pass();

		this->position_history.emplace_back(this->_position);

		if (move == BoardCoordinate::PASS)	
		{
			this->_position.pass();
			on_updated_position(BoardCoordinate::PASS);
			return true;
		}

		if (!this->_position.update(move))
		{
			this->position_history.pop_back();
			return false;
		}

		on_updated_position(move);
		return true;
	}

	bool Engine::undo_position()
	{
		if (!this->position_history.size())
			return false;

		this->_position = this->position_history.back();	
		this->position_history.pop_back();
		on_undid_position();
		return true;
	}

	bool Engine::set_option(const string& name, const string& value, string& err_msg)
	{
		if (!this->options.count(name))
		{
			err_msg = "invalid option.";
			return false;
		}

		this->options[name] = value;
		err_msg = this->options[name].last_err_msg();
		return err_msg.empty();
	}

	void Engine::get_options(EngineOptions& options)
	{
		for (auto& option : this->options)
			options.emplace_back(option);
	}
}