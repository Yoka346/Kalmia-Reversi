#include "kalmia.h"

#include <cmath>
#include <algorithm>
#include <filesystem>

#include "../config.h"

//#define SHOW_LOG	

using namespace std;
using namespace std::chrono;
using namespace std::filesystem;

using namespace utils;
using namespace io;
using namespace reversi;
using namespace search::mcts;
using namespace search::endgame;

namespace engine
{
	string search_end_status_to_string(SearchEndStatus status)
	{
		ostringstream oss;
		if ((status & SearchEndStatus::EXTENDED) == SearchEndStatus::EXTENDED)
		{
			oss << "extended + ";
			status ^= SearchEndStatus::EXTENDED;
		}

		switch (status)
		{
		case SearchEndStatus::COMPLETE:
			oss << "completed.";
			break;

		case SearchEndStatus::PROVED:
			oss << "proved.";
			break;

		case SearchEndStatus::TIMEOUT:
			oss << "timeout.";
			break;

		case SearchEndStatus::SUSPENDED_BY_STOP_SIGNAL:
			oss << "suspended.";
			break;

		case SearchEndStatus::OVER_NODES:
			oss << "over nodes.";
			break;

		case SearchEndStatus::EARLY_STOPPING:
			oss << "early stopping.";
			break;
		}
		return oss.str();
	}

	Kalmia::Kalmia()
		: Engine(NAME, VERSION, AUTHOR), logger(new Logger("")), logger_mutex()
	{
		this->_score_type = EvalScoreType::WIN_RATE;
		init_options();
	}

	// ToDo: 1手の時間を固定するオプションの追加.
	void Kalmia::init_options()
	{
		using namespace placeholders;

		// サーバーやGUIと通信する際の遅延[ms]. 
		this->options["latency_ms"] = EngineOption(50, 0, INT32_MAX, this->options.size());

		// 価値関数のパラメータファイルの場所.
		ostringstream vf_weight_path;
		vf_weight_path << EVAL_DIR << "value_func_weight.bin";
		this->options["value_func_weight_path"] =
			EngineOption(vf_weight_path.str(), this->options.size(),
						 bind(&Kalmia::on_value_func_weight_path_changed, this, _1, _2));

		// 探索スレッド数
		this->options["thread_num"] = 
			EngineOption(UCTOptions().thread_num,
						 1, thread::hardware_concurrency(), this->options.size(), bind(&Kalmia::on_thread_num_changed, this, _1, _2));

		// ノード数の上限
		this->options["node_num_limit"] =
			EngineOption(UCTOptions().node_num_limit,
						 1000000, INT32_MAX, this->options.size(), bind(&Kalmia::on_node_num_limit_changed, this, _1, _2));

		// 1回の思考における探索イテレーションの回数.
		this->options["playout"] = EngineOption(3200000, 1, INT32_MAX, this->options.size());

		// 探索結果に応じた確率的な着手を何手目まで行うか.
		this->options["stochastic_move_num"] = EngineOption(0, 0, SQUARE_NUM - 4, this->options.size());

		// 確率的な着手を行う場合のソフトマックス温度(大きい値であればあるほど, 不利な手を打つ確率が高くなる.)
		this->options["softmax_temperature"] = EngineOption(1000, 0, INT32_MAX,  this->options.size());

		// 前回の探索結果を次の探索で使い回すかどうか.
		this->options["reuse_subtree"] = EngineOption(true, this->options.size());	// ToDo: reuse subtreeのon/offの実装.

		// 相手の手番中も思考を続行するかどうか.
		this->options["enable_pondering"] = EngineOption(false, this->options.size());	// ToDo: ponderingの実装.

		// これ以上探索しても最善手が変わらない場合, 探索を打ち切るかどうか.
		this->options["enable_early_stopping"] = EngineOption(true, this->options.size());

		// 必要な場合に探索を延長するかどうか.
		this->options["enable_extra_search"] = EngineOption(false, this->options.size());

		// 残り何手の時点で終盤完全読みを実行するか.
		this->options["endgame_move_num"] = EngineOption(-1, -1, 60, this->options.size(), bind(&Kalmia::on_endgame_move_num_changed, this, _1, _2));

		// 終盤完全読みの置換表のサイズ(MiB).
		this->options["endgame_tt_size_mib"] = EngineOption(DEFAULT_ENDGAME_SOLVER_TT_SIZE_MIB, 128, INT32_MAX, this->options.size(), bind(&Kalmia::on_endgame_tt_size_mib_changed, this, _1, _2));

		// 探索情報を何cs間隔で表示するか.
		this->options["show_search_info_interval_cs"] = EngineOption(10, 10, INT32_MAX, this->options.size(), bind(&Kalmia::on_show_search_info_interval_changed, this, _1, _2));

		// 思考ログの保存先
		ostringstream log_path;
		log_path << LOG_DIR << "kalmia.log";
		this->options["thought_log_path"] = EngineOption(log_path.str(), this->options.size(), bind(&Kalmia::on_thought_log_path_changed, this, _1, _2));
	}

	void Kalmia::quit()
	{
		if (this->tree.get() && this->tree->is_searching())
		{
			this->tree->send_stop_search_signal();
			write_log("Kalmia recieved quit signal. Current calculation will be suspended.\n");
		}
		this->logger->flush();
	}

	void Kalmia::set_main_time(DiscColor color, milliseconds main_time)
	{
		GameTimer& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		if (main_time >= timer.main_time())
			timer.set_main_time(main_time);
		else
			timer.set_main_time_left(main_time);
	}

	void Kalmia::set_byoyomi(DiscColor color, milliseconds byoyomi)
	{
		GameTimer& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		timer.set_byoyomi(byoyomi);
	}

	void Kalmia::set_byoyomi_stones(DiscColor color, int32_t byoyomi_stones)
	{
		GameTimer& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		if (byoyomi_stones >= timer.byoyomi_stones())
			timer.set_byoyomi_stones(byoyomi_stones);
		else
			timer.set_byoyomi_stones_left(byoyomi_stones);
	}

	void Kalmia::set_time_inc(DiscColor color, milliseconds inc)
	{
		GameTimer& timer = this->timer[static_cast<int32_t>(color)];
		if (timer.is_ticking())
			return;

		timer.set_increment(inc);
	}

	void Kalmia::set_level(int32_t level)
	{
		// KalmiaにおけるLevelの意味は以下の通り.
		// (1 <= level <= 60) min(4 * 2^level, 20000000) [playouts] かつ 終盤 level 手完全読み

		this->options["playout"] = to_string(min(4 << level, 20000000));
		this->options["endgame_move_num"] = to_string(level);
	}

	void Kalmia::set_book_contempt(int32_t contempt)
	{
		// ToDo: まだBookが未実装なので, Bookを実装したら中身を書く.
	}

	void Kalmia::add_current_game_to_book()
	{
		// ToDo: まだBookが未実装なので, Bookを実装したら中身を書く.
	}

	double Kalmia::get_eval_score_min()
	{
		return (this->position().empty_square_count() > this->options["endgame_move_num"])
			? 0.0 : -64.0;
	}

	double Kalmia::get_eval_score_max()
	{
		return (this->position().empty_square_count() > this->options["endgame_move_num"])
			? 100.0 : 64.0;
	}

	bool Kalmia::on_ready()
	{
		try
		{
			string value_func_weight_path = this->options["value_func_weight_path"];
			if (!exists(value_func_weight_path))
			{
				ostringstream oss;
				oss << "Cannot find value func weight file: \"" << value_func_weight_path << "\".";
				send_err_message(oss.str());
				return false;
			}

			if (this->logger)
				this->logger.reset();
			this->logger = make_unique<Logger>(this->options["thought_log_path"]);

			if (this->tree.get())
			{
				unique_ptr<UCT> prev_tree = move(this->tree);
				this->tree = make_unique<UCT>(prev_tree->options, value_func_weight_path);
			}
			else
				this->tree = make_unique<UCT>(UCTOptions(), value_func_weight_path);

			this->tree->options.search_info_update_interval_cs = this->options["show_search_info_interval_cs"];
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

		update_score_type();
	}

	void Kalmia::on_position_was_set()
	{
		stop_if_pondering();
		this->tree->set_root_state(position());
		write_log("Tree was cleared.\n");

		update_score_type();
	}

	void Kalmia::on_updated_position(BoardCoordinate move)
	{
		stop_if_pondering();

		if (!this->tree->transition_root_state_to_child_state(move))
			this->tree->set_root_state(position());

		this->logger->flush();

		update_score_type();
	}

	void Kalmia::on_undid_position()
	{
		stop_if_pondering();

		this->tree->set_root_state(position());
		write_log("Undo.\n");
		write_log("Tree was cleared.\n");
	}

	void Kalmia::go(bool ponder)
	{
		// ToDo: 時間制御の実装

		stop_if_pondering();	

		this->suspend_search_flag = false;

		if (position().can_pass())
		{
			EngineMove move;
			move.coord = BoardCoordinate::PASS;
			send_move(move);
			return;
		}

		generate_mid_game_move(ponder);
	}

	void Kalmia::analyze(int32_t move_num)
	{
		stop_if_pondering();

		if (position().can_pass())
		{
			this->on_analysis_ended();
			return;
		}

		analyze_mid_game();
	}

	bool Kalmia::stop_thinking(milliseconds timeout)
	{
		if (!this->tree)
			return true;

		write_log("Recieved stop search signal.\n");

		this->tree->send_stop_search_signal();
		return !this->search_task.valid() || this->search_task.wait_for(timeout) == future_status::ready;
	}

	bool Kalmia::search_task_is_completed()
	{
		return this->search_task.wait_for(milliseconds::zero()) == future_status::ready;
	}

	void Kalmia::stop_if_pondering()
	{
		if (this->search_task.valid() && !search_task_is_completed())
		{
			this->tree->send_stop_search_signal();
			write_log("Stop pondering.\n\n");
			write_log(search_info_to_string(this->tree->search_info()));
		}
	}

	void Kalmia::write_log(const std::string& str)
	{
		lock_guard<mutex> lock(this->logger_mutex);
		if (this->logger)
		{
			*this->logger << str;
			this->logger->flush();
		}
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

		for (const MoveEvaluation& child_eval : search_info.child_evals)
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

	void Kalmia::send_mid_search_info(const SearchInfo& search_info)
	{
		ThinkInfo think_info;
		collect_think_info(search_info, think_info);
		send_think_info(think_info);

		MultiPV multi_pv;
		collect_multi_pv(search_info, multi_pv);
		send_multi_pv(multi_pv);

		write_log(search_info_to_string(search_info));
		write_log("\n");
	}

	void Kalmia::collect_think_info(const search::mcts::SearchInfo& search_info, ThinkInfo& think_info)
	{
		if (search_info.child_evals.size() == 0)
			return;

		think_info.ellapsed_ms = this->tree->search_ellapsed_ms();
		think_info.node_count = this->tree->node_count();
		think_info.nps = this->tree->nps();
		think_info.depth = search_info.child_evals[0].pv.size();	// 深さは最も有望なPVの深さにする.
		think_info.eval_score = search_info.root_eval.expected_reward * 100.0;
	}

	void Kalmia::collect_multi_pv(const search::mcts::SearchInfo& search_info, MultiPV& multi_pv)
	{
		for (const MoveEvaluation& child_eval : search_info.child_evals)
		{
			if (isnan(child_eval.expected_reward))
				continue;

			MultiPVItem item;
			// 厳密にはnode_count != playout_countだが, ここではplayout_countを使う.
			item.node_count = child_eval.playout_count;
			item.eval_score = child_eval.expected_reward * 100.0;
			item.eval_score_type = EvalScoreType::WIN_RATE;
			item.pv = child_eval.pv;
			multi_pv.emplace_back(item);
		}
	}

	void Kalmia::generate_mid_game_move(bool ponder)
	{
		write_log("Start search.\n");

		auto search_end_callback = [=, this](SearchEndStatus status)
		{
			write_log(search_end_status_to_string(status));
			write_log("\n");
			write_log("End search.\n");

			auto search_info = this->tree->search_info();
			write_log(search_info_to_string(search_info)); 
			EngineMove move;
			select_move(search_info, move);
			send_move(move);
		};

		this->tree->on_search_info_was_updated = [this](const auto& search_info) { send_mid_search_info(search_info); };
		this->search_task = this->tree->search_async(this->options["playout"], search_end_callback);
	}

	void Kalmia::analyze_mid_game()
	{
		write_log("Start search.\n");

		auto search_end_callback = [=, this](SearchEndStatus status)
		{
			write_log(search_end_status_to_string(status));
			write_log("\n");
			write_log("End search.\n");

			auto search_info = this->tree->search_info();
			write_log(search_info_to_string(search_info));

			send_mid_search_info(search_info);
			this->logger->flush();

			if (this->options["enable_early_stopping"])
				this->tree->enable_early_stopping();

			this->on_analysis_ended();
		};

		this->tree->on_search_info_was_updated = [this](const auto& search_info) { send_mid_search_info(search_info); };
		this->tree->disable_early_stopping();
		this->search_task = this->tree->search_async(this->options["playout"], INT32_MAX / 10, 0, search_end_callback);
	}

	void Kalmia::select_move(const SearchInfo& search_info, EngineMove& move)
	{
		if (search_info.child_evals.size() == 1)
		{
			const MoveEvaluation& child_eval = search_info.child_evals[0];
			move.coord = child_eval.move;
			move.eval_score = child_eval.expected_reward;
			return;
		}

		auto& child_evals = search_info.child_evals;
		auto selected_idx = 0;
		auto move_num = (SQUARE_NUM - 4) - position().empty_square_count() + 1;
		if (move_num <= this->options["stochastic_move_num"])	// プレイアウト数に応じた確率的着手
		{
			auto t_inv = 1.0 / (this->options["softmax_temperature"] * 1.0e-3);
			DynamicArray<size_t> indices(child_evals.size());
			DynamicArray<double> exp_po_counts(child_evals.size());
			auto exp_po_count_sum = 0.0;

			for (size_t i = 0; i < indices.length(); i++)
			{
				indices[i] = i;
				exp_po_count_sum += exp_po_counts[i] = pow(child_evals[i].playout_count, t_inv);
			}

			auto arrow = this->rand.next_double() * exp_po_count_sum;
			auto sum = 0.0;
			// child_evalsはプレイアウト数に応じた降順に並んでいるので, 着手が偏らないようにシャッフル.
			shuffle(indices.begin(), indices.end(), this->rand.generator());	
			for (auto i : indices)	// (プレイアウト数)^(温度)の大きさに応じて確率的に選択.
				if ((sum += exp_po_counts[selected_idx = i]) >= arrow)
					break;
		}
		move.coord = child_evals[selected_idx].move;
		move.eval_score = child_evals[selected_idx].expected_reward * 100.0f;
	}

	void Kalmia::update_score_type() 
	{
		this->_score_type = EvalScoreType::WIN_RATE;
		/*if (position().empty_square_count() > this->options["endgame_move_num"])	// 終盤探索が完成したらコメントアウト
			this->_score_type = EvalScoreType::WIN_RATE;
		else
			this->_score_type = EvalScoreType::DISC_DIFF;*/
	}

	void Kalmia::on_value_func_weight_path_changed(EngineOption& sender, std::string& err_message)
	{
		if (this->tree.get() && this->tree->is_searching())
		{
			err_message = "Cannnot set value_func_weight_path while searching.";
			return;
		}

		if (state() != EngineState::NOT_READY)
		{
			unique_ptr<UCT> prev_tree = move(this->tree);
			this->tree = make_unique<UCT>(prev_tree->options, sender);
		}
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

	void Kalmia::on_endgame_move_num_changed(EngineOption& sender, string& err_message)
	{
		this->_score_type = (this->position().empty_square_count() > this->options["endgame_move_num"])
			? EvalScoreType::WIN_RATE
			: EvalScoreType::DISC_DIFF;
	}

	void Kalmia::on_endgame_tt_size_mib_changed(EngineOption& sender, string& err_message)
	{
		// ToDo: 終盤探索の実装.
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

	void Kalmia::on_show_search_info_interval_changed(EngineOption& sender, std::string& err_message)
	{
		this->tree->options.search_info_update_interval_cs = sender;
	}

	void Kalmia::on_thought_log_path_changed(EngineOption& sender, std::string& err_message)
	{
		auto logger = make_unique<Logger>(sender.current_value());
		if (!sender.current_value().empty() && !logger->is_valid())
		{
			ostringstream oss;
			oss << "Cannnot open \"" << sender << "\".";
			err_message = oss.str();
			return;
		}

		lock_guard<mutex> lock(this->logger_mutex);
		if (this->logger)
			unique_ptr<Logger> tmp = move(this->logger);
		this->logger = move(logger);
	}
}