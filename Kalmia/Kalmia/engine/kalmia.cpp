#include "kalmia.h"

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace io;
using namespace reversi;
using namespace search::mcts;

namespace engine
{
	Kalmia::Kalmia(UCTOptions tree_options, const std::string& log_file_path)
		: Engine(NAME, VERSION),
		options(), tree(tree_options), rand(), logger(log_file_path) { }

	Kalmia::Kalmia(UCTOptions tree_options, const std::string& log_file_path, std::ostream* log_out)
		: Engine(NAME, VERSION),
		options(), tree(tree_options), rand(), logger(log_file_path, log_out) { }

	void Kalmia::init_options()
	{
		using namespace placeholders;

		// サーバーやGUIと通信する際の遅延. 
		this->options["latency_ms"] = EngineOption(50, 0, INT32_MAX, this->options.size());

		// 1回の思考における探索イテレーションの回数.
		this->options["playout"] = EngineOption(320000, 1, INT32_MAX, this->options.size());

		// 探索結果に応じた確率的な着手を何手目まで行うか.
		this->options["stochastic_move_num"] = EngineOption(0, 0, SQUARE_NUM - 4, this->options.size());

		// 確率的な着手を行う場合のソフトマックス温度(1より高い値であればあるほど, 不利な手を打つ確率が高くなる.)
		this->options["softmax_temperture"] = EngineOption("0.0", this->options.size(), bind(&Kalmia::on_softmax_temperture_changed, this, _1, _2));

		// 過去の探索結果を次の探索で使い回すかどうか.
		this->options["reuse_subtree"] = EngineOption(true, this->options.size());

		// 相手の手番中も思考を続行するかどうか.
		this->options["enable_pondering"] = EngineOption(false, this->options.size());
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

		if (this->think_task.valid() && think_task_is_completed())
		{
			stop_pondering();
			write_log("Stop pondering.\n\n");
			string search_info_str;
			get_search_info_string(search_info_str);
			write_log(search_info_str);
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

		if (this->think_task.valid() && !think_task_is_completed())
			stop_pondering();

		this->tree.set_root_state(this->_position);
		write_log("Undo.\n");
		write_log("Tree was cleared.\n");
	}

	bool Kalmia::think_task_is_completed()
	{
		return (this->think_task.wait_for(milliseconds::zero()) == future_status::ready);
	}

	void Kalmia::stop_pondering()
	{
		this->tree.send_stop_search_signal();
		this->think_task.wait();
	}

	void Kalmia::write_log(const std::string& str)
	{
		this->logger << str;
		send_text_message(str);
	}

	void Kalmia::get_search_info_string(std::string& str)
	{
		auto& search_info = this->tree.search_info();
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
		str = oss.str();
	}

	void Kalmia::on_softmax_temperture_changed(const EngineOption& sender, string& err_message)
	{
		auto& current_value = sender.current_value();
		try
		{
			this->softmax_temperture = std::stod(current_value);
		}
		catch (std::invalid_argument)
		{
			ostringstream oss;
			oss << "Invalid value \"" << current_value << "\" was specified to \"softmax_temperture\".";
			oss << " it must be real number which is more than or equal 0.";
			err_message = oss.str();
		}
	}
}