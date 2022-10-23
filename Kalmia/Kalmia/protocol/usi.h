#pragma once
#include <iostream>
#include <sstream>
#include <fstream>
#include <string>
#include <map>
#include <functional>
#include <mutex>
#include <future>
#include <atomic>

#include "../io/sync_stream.h"
#include "../reversi/constant.h"
#include "../engine/engine.h"

namespace protocol
{
	class USI
	{
	public:
		using CommandHandler = std::function<void(std::istringstream&)>;
		using CommandMap = std::map<std::string, CommandHandler>;

		USI(std::istream* usi_in = &std::cin, std::ostream* usi_out = &std::cout) : usi_in(usi_in), usi_out(usi_out) { init(); }
		void mainloop(engine::Engine* engine, const std::string& log_file_path);
		void mainloop(engine::Engine* engine) { std::string null_path = ""; mainloop(engine, null_path); }
		
	private:
		engine::Engine* engine;
		CommandMap commands;
		bool quit_flag = true;
		std::istream* usi_in;
		io::SyncOutStream usi_out;
		std::ofstream logger;
		std::mutex mainloop_mutex;
		std::future<reversi::BoardCoordinate> go_command_future;
		std::atomic<bool> stop_go_flag;

		void init();
		void on_think_info_is_sent(const engine::ThinkInfo&);
		void on_multi_pv_is_sent(const engine::MultiPV&);
		CommandHandler to_handler(void (USI::* exec_cmd)(std::istringstream&));
		void usi_failure(const std::string msg);

		bool go_command_has_done()
		{ 
			return !this->go_command_future.valid()
				|| this->go_command_future.wait_for(std::chrono::milliseconds::zero()) == std::future_status::ready;
		}

		void set_time(const std::string time_kind, const std::string time);

		void exec_usi_command(std::istringstream&);
		void exec_isready_command(std::istringstream&);
		void exec_setoption_command(std::istringstream&);
		void exec_usinewgame_command(std::istringstream&);
		void exec_position_command(std::istringstream&);
		void exec_go_command(std::istringstream&);
		void exec_stop_command(std::istringstream&);
		void exec_ponderhit_command(std::istringstream&);
		void exec_quit_command(std::istringstream&);
		void exec_gameover_command(std::istringstream&);
		void exec_score_scale_and_type_command(std::istringstream&);
	};
}
