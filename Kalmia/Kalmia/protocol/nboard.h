#pragma once

#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <functional>
#include <mutex>
#include <future>
#include <atomic>

#include "protocol.h"
#include "../engine/engine.h"
#include "../reversi/types.h"
#include "../io/sync_stream.h"

namespace protocol
{
	class NBoard : public IProtocol
	{
	public:
		static constexpr int32_t PROTOCOL_VERSION = 2;
		using CommandHandler = std::function<void(std::istringstream&)>;
		using CommandMap = std::map<std::string, CommandHandler>;

		NBoard(std::istream* nb_in = &std::cin, std::ostream* nb_out = &std::cout, std::ostream* nb_err = &std::cerr) 
			: nb_in(nb_in), nb_out(nb_out), nb_err(nb_err)
		{
			init_handlers();
		}

		void mainloop(engine::Engine* engine, const std::string& log_file_path) override;
		void mainloop(engine::Engine* engine) { mainloop(engine, ""); }

	private:
		static constexpr int32_t TIMEOUT_MS = 10000;

		engine::Engine* engine = nullptr;
		CommandMap commands;
		std::istream* nb_in;
		io::SyncOutStream nb_out;
		io::SyncOutStream nb_err;
		io::SyncOutStream* logger;
		std::mutex mainloop_mutex;
		int32_t hint_num = 1;
		std::atomic<bool> engine_is_thinking = false;	// goやhint, analyzeコマンドが実行中かどうか.
		bool quit_flag = false;

		void init_handlers();
		void init_engine(engine::Engine* engine);
		CommandHandler to_handler(void (NBoard::* exec_cmd)(std::istringstream&));
		void nboard_success(const std::string& responce);
		void nboard_failure(const std::string& msg);

		void send_node_stats(const engine::ThinkInfo& think_info);
		void send_hints(const engine::MultiPV& multi_pv);
		void send_move(const engine::EngineMove& move);

		void exec_nboard_command(std::istringstream&);
		void exec_set_command(std::istringstream&);
		void exec_move_command(std::istringstream&);
		void exec_hint_command(std::istringstream&);
		void exec_go_command(std::istringstream&);
		void exec_ping_command(std::istringstream&);
		void exec_learn_command(std::istringstream&);
		void exec_analyze_command(std::istringstream&);
		void exec_quit_command(std::istringstream&);
	};
}
