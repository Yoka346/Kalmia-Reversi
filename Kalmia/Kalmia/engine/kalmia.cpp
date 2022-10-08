#include "kalmia.h"

#include <algorithm>

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace io;
using namespace reversi;
using namespace search::mcts;

namespace engine
{
	string search_end_status_to_string(SearchEndStatus status)
	{
		switch (status)
		{
		case SearchEndStatus::PROVED:
			return "proved.";

		case SearchEndStatus::TIMEOUT:
			return "timeout.";

		case SearchEndStatus::SUSPENDED_BY_STOP_SIGNAL:
			return "suspended.";

		case SearchEndStatus::SUSPENDED_DUE_TO_OVER_NODES:
			return "over nodes.";
		}
		return "early stopping.";
	}

	Kalmia::Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path)
		: Engine(NAME, VERSION), tree(value_func_param_file_path), logger(log_file_path)
	{
		init_options();
	}

	Kalmia::Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path, std::ostream* log_out)
		: Engine(NAME, VERSION), tree(value_func_param_file_path), logger(log_file_path, log_out)
	{
		init_options();
	}

	void Kalmia::init_options()
	{
		using namespace placeholders;

		// サーバーやGUIと通信する際の遅延. 
		this->options["latency_ms"] = EngineOption(50, 0, INT32_MAX, this->options.size());

		// 探索スレッド数
		this->options["thread_num"] = 
			EngineOption(this->tree.options.thread_num, 
						 1, thread::hardware_concurrency(), this->options.size(), bind(&Kalmia::on_thread_num_changed, this, _1, _2));

		// ノード数の上限
		this->options["node_num_limit"] =
			EngineOption(this->tree.options.node_num_limit,
						 1000, INT32_MAX, this->options.size(), bind(&Kalmia::on_node_num_limit_changed, this, _1, _2));

		// 1回の思考における探索イテレーションの回数.
		this->options["playout"] = EngineOption(320000, 1, INT32_MAX, this->options.size());

		// 探索結果に応じた確率的な着手を何手目まで行うか.
		this->options["stochastic_move_num"] = EngineOption(0, 0, SQUARE_NUM - 4, this->options.size());

		// 確率的な着手を行う場合のソフトマックス温度(1より高い値であればあるほど, 不利な手を打つ確率が高くなる.)
		this->options["softmax_temperature"] = EngineOption("0.0", this->options.size(), bind(&Kalmia::on_softmax_temperature_changed, this, _1, _2));

		// 過去の探索結果を次の探索で使い回すかどうか.
		this->options["reuse_subtree"] = EngineOption(true, this->options.size());

		// 相手の手番中も思考を続行するかどうか.
		this->options["enable_pondering"] = EngineOption(false, this->options.size());

		// これ以上探索しても最善手が変わらない場合, 探索を打ち切るかどうか.
		this->options["enable_early_stopping"] = EngineOption(true, this->options.size());

		// 必要な場合に探索を延長するかどうか.
		this->options["enable_additional_search"] = EngineOption(false, this->options.size());

		// 探索情報を何ms間隔で表示するか.
		this->options["show_search_info_interval_ms"] = EngineOption(5000, 100, INT32_MAX, this->options.size());
	}

	bool Kalmia::set_option(const string& name, const string& value, std::string& err_msg)
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

	void Kalmia::get_options(EngineOptions& options)
	{
		for (auto& option : this->options)
			options.emplace_back(option);
	}

	bool Kalmia::update_position(DiscColor color, BoardCoordinate coord)
	{
		if (!Engine::update_position(color, coord))
			return false;

		if (this->search_task.valid() && search_task_is_completed())
		{
			stop_pondering();
			write_log("Stop pondering.\n\n");
			write_log(search_info_to_string(this->tree.get_search_info()));
		}

		if (!this->tree.transition_root_state_to_child_state(coord))
			this->tree.set_root_state(this->_position);

		ostringstream oss;
		oss << "\nopponent's move is " << coordinate_to_string(coord) << "\n";
		write_log(oss.str());
		this->logger.flush();
		return true;
	}

	bool Kalmia::undo_position()
	{
		if (!Engine::undo_position())
			return false;

		if (this->search_task.valid() && !search_task_is_completed())
			stop_pondering();

		this->tree.set_root_state(this->_position);
		write_log("Undo.\n");
		write_log("Tree was cleared.\n");
		return true;
	}

	BoardCoordinate Kalmia::generate_move(DiscColor color)
	{
		if (this->search_task.valid() && !search_task_is_completed())
		{
			stop_pondering();
			write_log("Stop pondering.\n");
			write_log(search_info_to_string(this->tree.get_search_info()));
		}

		if (this->_position.side_to_move() != color)
		{
			this->_position.pass();
			this->tree.set_root_state(this->_position);
			write_log("Tree was cleared.\n");
		}

		return generate_mid_game_move(color);
	}

	bool Kalmia::stop_thinking(std::chrono::milliseconds timeout_ms)
	{
		this->tree.send_stop_search_signal();
		return this->search_task.wait_for(timeout_ms) == future_status::ready;
	}

	void Kalmia::quit()
	{
		if (this->tree.is_searching())
		{
			this->tree.send_stop_search_signal();
			write_log("Kalmia recieved quit signal. Current calculation will be suspended.\n");
		}
		this->logger.flush();
	}

	BoardCoordinate Kalmia::generate_mid_game_move(reversi::DiscColor color)
	{
		// ToDo: 時間制御を実装する.
		this->timer[color].start();
		this->_is_thinking = true;

		write_log("Start search.\n");

		this->search_task = this->tree.search_async(this->options["playout"]);
		wait_for_search();

		write_log(search_end_status_to_string(this->search_task.get()));
		write_log("\n");
		write_log("End search.\n");

		auto& search_info = this->tree.get_search_info();
		write_log(search_info_to_string(search_info));

		using namespace std::placeholders;
		auto move_num = (SQUARE_NUM - 4) - this->_position.empty_square_count() + 1;
		auto move_selector = 
			(move_num <= this->options["stochastic_move_num"])
			? &Kalmia::select_move<MoveSelection::STOCHASTICALLY>
			: &Kalmia::select_move<MoveSelection::BEST>;
		auto select_move = bind(move_selector, this, _1, _2);
		bool extra_search_is_needed;
		auto move = select_move(search_info, extra_search_is_needed);

		if (extra_search_is_needed)
		{
			write_log("\nStart extra search.\n");
			this->logger.flush();

			this->search_task = this->tree.search_async(this->options["playout"]);
			wait_for_search();

			write_log(search_end_status_to_string(this->search_task.get()));
			write_log("\n");
			write_log("End extra search.\n");

			auto& new_search_info = this->tree.get_search_info();
			move = select_move(new_search_info, extra_search_is_needed);

			write_log(search_info_to_string(new_search_info));
		}

		this->logger.flush();
		this->timer[color].stop();
		this->_is_thinking = false;
		return move;
	}

	void Kalmia::wait_for_search()
	{
		milliseconds show_search_info_interval_ms(this->options["show_search_info_interval_ms"]);
		auto start_time = high_resolution_clock::now();
		while (this->tree.is_searching())
		{
			this_thread::sleep_for(milliseconds(10));
			
			auto time_now = high_resolution_clock::now();
			auto& search_info = this->tree.get_search_info();
			if (duration_cast<milliseconds>(time_now - start_time) >= show_search_info_interval_ms)
			{
				write_log(search_info_to_string(search_info));
				start_time = time_now;
			}
		}
		this->search_task.wait();
	}

	template<MoveSelection MOVE_SELECT>
	BoardCoordinate Kalmia::select_move(const SearchInfo& search_info, bool& extra_search_is_needed)
	{
		constexpr double ENOUGH_SEARCH_THRESHOLD = 1.5;

		if (search_info.child_evals.length() == 1)
		{
			extra_search_is_needed = false;
			return search_info.child_evals[0].move;
		}

		auto& child_evals = search_info.child_evals;

		// 確率的選択のときのみ用いる変数.
		double t_inv;
		DynamicArray<double> exp_po_counts(0);
		double exp_po_count_sum;

		if constexpr (MOVE_SELECT == MoveSelection::STOCHASTICALLY)
		{
			t_inv = 1.0 / this->softmax_temperature;
			exp_po_counts = DynamicArray<double>(child_evals.length());
			exp_po_count_sum = 0.0;
		}

		auto best_idx = 0;
		auto second_idx = 0;
		uint32_t max_po_count = 0;
		uint32_t second_po_count = 0;
		for (auto i = 0; i < child_evals.length(); i++)
		{
			auto& child_eval = child_evals[i];
			auto po_count = child_eval.playout_count;
			if (po_count >= max_po_count)
			{
				second_po_count = max_po_count;
				second_idx = best_idx;
				max_po_count = po_count;
				best_idx = i;
			}
			else if (po_count >= second_po_count)
			{
				second_po_count = po_count;
				second_idx = i;
			}

			if constexpr (MOVE_SELECT == MoveSelection::STOCHASTICALLY)
				exp_po_count_sum += exp_po_counts[i] = pow(po_count, t_inv);
		}

		auto selected_idx = 0;
		if constexpr (MOVE_SELECT == MoveSelection::STOCHASTICALLY)
		{
			auto arrow = this->rand.next_double() * exp_po_count_sum;
			auto sum = 0.0;
			for (; selected_idx < exp_po_counts.length() - 1; selected_idx++)	// (訪問回数)^(温度)の大きさに応じて確率的に選択.
				if ((sum += exp_po_counts[selected_idx]) >= arrow)
					break;
		}
		else
			selected_idx = best_idx;

		auto& best_child = child_evals[best_idx];
		auto& second_child = child_evals[second_idx];
		extra_search_is_needed =
			second_child.expected_reward > best_child.expected_reward	// 最善手と次善手で価値が逆転している場合は, 探索が不十分.
			|| second_child.playout_count * ENOUGH_SEARCH_THRESHOLD > best_child.playout_count;		// 最善手と次善手のプレイアウト回数に大きな開きがない場合は探索延長.
		return child_evals[selected_idx].move;
	}

	bool Kalmia::search_task_is_completed()
	{
		return (this->search_task.wait_for(milliseconds::zero()) == future_status::ready);
	}

	void Kalmia::stop_pondering()
	{
		this->tree.send_stop_search_signal();
		this->search_task.wait();
	}

	void Kalmia::write_log(const std::string& str)
	{
		this->logger << str;
		send_text_message(str);
	}

	string Kalmia::search_info_to_string(const SearchInfo& search_info)
	{
		ostringstream oss;
		oss << "ellapsed=" << this->tree.search_ellapsed_ms() << "[ms] ";
		oss << search_info.root_eval.playout_count << "[po] ";
		oss << this->tree.pps() << "[pps] ";
		oss << "winning_rate=" << fixed << setprecision(2) << search_info.root_eval.expected_reward * 100.0 << "%\n";
		oss << "|move|winning_rate|effort|playout count|depth|pv\n";

		for (auto& child_eval : search_info.child_evals)
		{
			oss << "| " << coordinate_to_string(child_eval.move) << " |";
			oss << "|" << right << setw(12) << fixed << setprecision(2) << child_eval.expected_reward * 100.0 << "%|";
			oss << "|" << right << setw(6) << fixed << setprecision(2) << child_eval.effort * 100.0 << "%|";
			oss << "|" << right << setw(13) << child_eval.playout_count << "|";
			oss << "|" << right << setw(5) << child_eval.pv.size() << "|";
			oss << "|";
			for (auto& move : child_eval.pv)
				oss << move << " ";
			oss << "\n";
		}
		return oss.str();
	}

	void Kalmia::on_thread_num_changed(EngineOption& sender, std::string& err_message)
	{
		this->tree.options.thread_num = sender;
	}

	void Kalmia::on_node_num_limit_changed(EngineOption& sender, std::string& err_message)
	{
		this->tree.options.node_num_limit = sender;
	}

	void Kalmia::on_softmax_temperature_changed(EngineOption& sender, string& err_message)
	{
		auto& current_value = sender.current_value();
		try
		{
			this->softmax_temperature = std::stod(current_value);
			if (this->softmax_temperature <= 0.0)
				throw invalid_argument("");
		}
		catch (invalid_argument)
		{
			ostringstream oss;
			oss << "Invalid value \"" << current_value << "\" was specified to \"softmax_temperature\".";
			oss << " It must be real number which is more than 0.0.";
			err_message = oss.str();

			sender = sender.default_value();
		}
	}
}