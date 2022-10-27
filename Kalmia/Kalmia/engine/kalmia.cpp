#include "kalmia.h"

#include <algorithm>
#include <filesystem>

#include "../config.h"

#define SHOW_LOG	

using namespace std;
using namespace std::chrono;
using namespace std::filesystem;

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

		case SearchEndStatus::OVER_NODES:
			return "over nodes.";
		}
		return "early stopping.";
	}

	Kalmia::Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path)
		: Engine(NAME, VERSION, AUTHOR), logger(log_file_path)
	{
		init_options();
	}

	Kalmia::Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path, std::ostream* log_out)
		: Engine(NAME, VERSION, AUTHOR), logger(log_file_path, log_out)
	{
		init_options();
	}

	void Kalmia::init_options()
	{
		using namespace placeholders;

		// サーバーやGUIと通信する際の遅延. 
		this->options["latency_ms"] = EngineOption(50, 0, INT32_MAX, this->options.size());

		ostringstream oss;
		oss << EVAL_DIR << "value_func_weight.bin";
		this->options["value_func_weight_path"] =
			EngineOption(oss.str().c_str(), this->options.size());

		// 探索スレッド数
		this->options["thread_num"] = 
			EngineOption(UCTOptions().thread_num,
						 1, thread::hardware_concurrency(), this->options.size(), bind(&Kalmia::on_thread_num_changed, this, _1, _2));

		// ノード数の上限
		this->options["node_num_limit"] =
			EngineOption(UCTOptions().node_num_limit,
						 1000, INT32_MAX, this->options.size(), bind(&Kalmia::on_node_num_limit_changed, this, _1, _2));

		// 1回の思考における探索イテレーションの回数.
		this->options["playout"] = EngineOption(3200000, 1, INT32_MAX, this->options.size());

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
		this->options["enable_extra_search"] = EngineOption(false, this->options.size());

		// 探索情報を何cs間隔で表示するか.
		this->options["show_search_info_interval_cs"] = EngineOption(500, 10, INT32_MAX, this->options.size());
	}

	void Kalmia::quit()
	{
		if (this->tree.get() && this->tree->is_searching())
		{
			this->tree->send_stop_search_signal();
			write_log("Kalmia recieved quit signal. Current calculation will be suspended.\n");
		}
		this->logger.flush();
	}

	void Kalmia::set_main_time(DiscColor color, milliseconds main_time)
	{
		auto& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		if (main_time >= timer.main_time())
			timer.set_main_time(main_time);
		else
			timer.set_main_time_left(main_time);
	}

	void Kalmia::set_byoyomi(DiscColor color, milliseconds byoyomi)
	{
		auto& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		timer.set_byoyomi(byoyomi);
	}

	void Kalmia::set_byoyomi_stones(DiscColor color, int32_t byoyomi_stones)
	{
		auto& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		if (byoyomi_stones >= timer.byoyomi_stones())
			timer.set_byoyomi_stones(byoyomi_stones);
		else
			timer.set_byoyomi_stones_left(byoyomi_stones);
	}

	void Kalmia::set_time_inc(DiscColor color, milliseconds inc)
	{
		auto& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		timer.set_increment(inc);
	}

	bool Kalmia::on_ready()
	{
		try
		{
			string value_func_param_path = this->options["value_func_weight_path"].current_value();
			if (!exists(value_func_param_path))
			{
				ostringstream oss;
				oss << "Cannot find value func weight file: \"" << value_func_param_path << "\"";
				send_err_message(oss.str());
				return false;
			}

			if (this->tree.get())
			{
				auto prev_tree = move(this->tree);
				this->tree = make_unique<UCT>(prev_tree->options, value_func_param_path);
			}
			else
				this->tree = make_unique<UCT>(UCTOptions(), value_func_param_path);
		}
		catch (invalid_argument ex)
		{
			send_err_message(ex.what());
		}

		this->tree->set_root_state(position());

		return true;
	}

	void Kalmia::on_cleared_position() 
	{ 
		stop_if_pondering();
		this->tree->set_root_state(position());
		write_log("Tree was cleared.\n");
	}

	void Kalmia::on_position_was_set()
	{
		stop_if_pondering();
		this->tree->set_root_state(position());
		write_log("Tree was cleared.\n");
	}

	void Kalmia::on_updated_position(BoardCoordinate move)
	{
		stop_if_pondering();

		if (!this->tree->transition_root_state_to_child_state(move))
			this->tree->set_root_state(position());

		this->logger.flush();
	}

	void Kalmia::on_undid_position()
	{
		if (this->search_task.valid() && !search_task_is_completed())
			stop_pondering();

		this->tree->set_root_state(position());
		write_log("Undo.\n");
		write_log("Tree was cleared.\n");
	}

	bool Kalmia::on_stop_thinking(std::chrono::milliseconds timeout)
	{
		this->tree->send_stop_search_signal();
		return this->search_task.wait_for(timeout) == future_status::ready;
	}

	BoardCoordinate Kalmia::generate_move(bool ponder)
	{
		// ToDo: 時間制御の実装
		stop_if_pondering();

		if (position().can_pass())
			return BoardCoordinate::PASS;

		return generate_mid_game_move(ponder);
	}

	bool Kalmia::search_task_is_completed()
	{
		return this->search_task.wait_for(milliseconds::zero()) == future_status::ready;
	}

	void Kalmia::stop_pondering()
	{
		this->tree->send_stop_search_signal();
		this->search_task.wait();
	}

	void Kalmia::stop_if_pondering()
	{
		if (this->search_task.valid() && !search_task_is_completed())
		{
			stop_pondering();
			write_log("Stop pondering.\n\n");
			write_log(search_info_to_string(this->tree->get_search_info()));
		}
	}

	void Kalmia::write_log(const std::string& str)
	{
		this->logger << str;
#ifdef SHOW_LOG
		send_text_message(str);
#endif
	}

	string Kalmia::search_info_to_string(const SearchInfo& search_info)
	{
		ostringstream oss;
		oss << "ellapsed=" << this->tree->search_ellapsed_ms() << "[ms] ";
		oss << this->tree->node_count() << "[nodes] ";
		oss << this->tree->nps() << "[nps] ";
		oss << search_info.root_eval.playout_count << "[po] ";
		oss << "winning_rate=" << fixed << setprecision(2) << search_info.root_eval.expected_reward * 100.0 << "%\n";
		oss << "|move|win_rate|effort|simulation|depth|pv\n";

		for (auto& child_eval : search_info.child_evals)
		{
			oss << "| " << coordinate_to_string(child_eval.move) << " ";
			oss << "|" << right << setw(7) << fixed << setprecision(2) << child_eval.expected_reward * 100.0 << "%";
			oss << "|" << right << setw(5) << fixed << setprecision(2) << child_eval.effort * 100.0 << "%";
			oss << "|" << right << setw(10) << child_eval.playout_count;
			oss << "|" << right << setw(5) << child_eval.pv.size();
			oss << "|";
			for (auto& move : child_eval.pv)
				oss << coordinate_to_string(move) << " ";
			oss << "\n";
		}
		return oss.str();
	}

	void Kalmia::send_all_search_info()
	{
		auto& search_info = this->tree->get_search_info();
		ThinkInfo think_info;
		collect_think_info(search_info, think_info);
		send_think_info(think_info);

		MultiPV multi_pv;
		collect_multi_pv(search_info, multi_pv);
		send_multi_pv(multi_pv);

		write_log(search_info_to_string(search_info));
	}

	void Kalmia::collect_think_info(const search::mcts::SearchInfo& search_info, ThinkInfo& think_info)
	{
		think_info.ellapsed_ms = this->tree->search_ellapsed_ms();
		think_info.node_count = this->tree->node_count();
		think_info.nps = this->tree->nps();
		think_info.depth = search_info.child_evals[0].pv.size();	// 深さは最も有望なPVの深さにする.
	}

	void Kalmia::collect_multi_pv(const search::mcts::SearchInfo& search_info, MultiPV& multi_pv)
	{
		for (auto& child_eval : search_info.child_evals)
		{
			MultiPVItem item;
			// 厳密にはnode_count != playout_countだが, ここではplayout_countを使う.
			item.node_count = child_eval.playout_count;
			item.eval_score = child_eval.expected_reward * 100.0;
			item.pv = child_eval.pv;
			multi_pv.emplace_back(item);
		}
	}

	BoardCoordinate Kalmia::generate_mid_game_move(bool ponder)
	{
		write_log("Start search.\n");

		this->search_task = this->tree->search_async(this->options["playout"]);
		wait_for_mid_search();

		write_log(search_end_status_to_string(this->search_task.get()));
		write_log("\n");
		write_log("End search.\n");

		auto& search_info = this->tree->get_search_info();
		write_log(search_info_to_string(search_info));

		using namespace std::placeholders;
		auto move_num = (SQUARE_NUM - 4) - position().empty_square_count() + 1;
		auto move_selector = 
			(move_num <= this->options["stochastic_move_num"])
			? &Kalmia::select_move<MoveSelection::STOCHASTICALLY>
			: &Kalmia::select_move<MoveSelection::BEST>;
		auto select_move = bind(move_selector, this, _1, _2);
		bool extra_search_is_needed;
		auto move = select_move(search_info, extra_search_is_needed);

		if (extra_search_is_needed && this->options["enable_extra_search"])
		{
			write_log("\nStart extra search.\n");
			this->logger.flush();

			this->search_task = this->tree->search_async(this->options["playout"]);
			wait_for_mid_search();

			write_log(search_end_status_to_string(this->search_task.get()));
			write_log("\n");
			write_log("End extra search.\n");

			auto& new_search_info = this->tree->get_search_info();
			move = select_move(new_search_info, extra_search_is_needed);

			write_log(search_info_to_string(new_search_info));
		}

		send_all_search_info();
		this->logger.flush();
		return move;
	}

	void Kalmia::wait_for_mid_search()
	{
		milliseconds show_search_info_interval_ms(this->options["show_search_info_interval_cs"] * 10);
		auto start_time = high_resolution_clock::now();
		while (this->search_task.wait_for(milliseconds::zero()) != future_status::ready)
		{
			this_thread::sleep_for(milliseconds(10));
			
			auto time_now = high_resolution_clock::now();
			if (duration_cast<milliseconds>(time_now - start_time) >= show_search_info_interval_ms)
			{
				send_all_search_info();
				start_time = time_now;
			}
		}
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
		auto selected_idx = 0;

		if constexpr (MOVE_SELECT == MoveSelection::STOCHASTICALLY)
		{
			auto t_inv = 1.0 / this->softmax_temperature;
			DynamicArray<int32_t> indices(child_evals.length());
			DynamicArray<double> exp_po_counts(child_evals.length());
			auto exp_po_count_sum = 0.0;

			for (auto i = 0; i < indices.length(); i++)
			{
				indices[i] = i;
				exp_po_count_sum += exp_po_counts[i] = pow(child_evals[i].playout_count, t_inv);
			}

			auto arrow = this->rand.next_double() * exp_po_count_sum;
			auto sum = 0.0;
			shuffle(indices.begin(), indices.end(), this->rand.generator());
			for (auto i : indices)	// (訪問回数)^(温度)の大きさに応じて確率的に選択.
				if ((sum += exp_po_counts[selected_idx = i]) >= arrow)
					break;
		}

		auto& best_child = child_evals[0];
		auto& second_child = child_evals[1];
		extra_search_is_needed =
			second_child.expected_reward > best_child.expected_reward	// 最善手と次善手で価値が逆転している場合は, 探索が不十分.
			|| second_child.playout_count * ENOUGH_SEARCH_THRESHOLD > best_child.playout_count;		// 最善手と次善手のプレイアウト回数に大きな開きがない場合は探索延長.
		return child_evals[selected_idx].move;
	}

	void Kalmia::on_thread_num_changed(EngineOption& sender, std::string& err_message)
	{
		if (this->tree.get())
			this->tree->options.thread_num = sender;
		else
			err_message = "Tree was not initialized.";
	}

	void Kalmia::on_node_num_limit_changed(EngineOption& sender, std::string& err_message)
	{
		if (this->tree.get())
			this->tree->options.node_num_limit = sender;
		else
			err_message = "Tree was not initialized.";
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

	void Kalmia::on_enable_early_stopping_changed(EngineOption& sender, string& err_message)
	{
		if (this->tree.get())
		{
			if (sender)
				this->tree->enable_early_stopping();
			else
				this->tree->disable_early_stopping();
		}
		else
			err_message = "Tree was not initialized.";
	}
}