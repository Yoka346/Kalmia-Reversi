#pragma once
#include <chrono>

namespace utils
{
	struct GameTimerOptions
	{
		std::chrono::milliseconds main_time_ms;
		std::chrono::milliseconds byoyomi_ms;
		std::chrono::milliseconds increment_ms;
		int32_t byoyomi_stones;
	};

	class GameTimer
	{
	public:
		GameTimer() : GameTimer(std::chrono::milliseconds::zero(), std::chrono::milliseconds::zero(), 1, std::chrono::milliseconds::zero()) { ; }

		GameTimer(std::chrono::milliseconds main_time_ms, std::chrono::milliseconds byoyomi_ms, int32_t byoyomi_stones, std::chrono::milliseconds inc_ms)
			: _main_time(main_time_ms), _byoyomi(byoyomi_ms), _byoyomi_stones(byoyomi_stones), _increment(inc_ms),
			  _time_left(main_time_ms + byoyomi_ms), _byoyomi_stones_left(byoyomi_stones), _is_ticking(false), _timeout(false) { ; }

		GameTimer(GameTimerOptions& options)
			: _main_time(options.main_time_ms), _byoyomi(options.byoyomi_ms), _byoyomi_stones(options.byoyomi_stones), 
			_increment(options.increment_ms), _time_left(options.main_time_ms + options.byoyomi_ms), 
			_byoyomi_stones_left(options.byoyomi_stones), _is_ticking(false), _timeout(false) { ; }

		std::chrono::milliseconds main_time() const { return this->_main_time; }
		std::chrono::milliseconds byoyomi() const { return this->_byoyomi; }
		int32_t byoyomi_stones() const { return this->_byoyomi_stones; }
		int32_t byoyomi_stones_left() const { return this->_byoyomi_stones_left; }
		std::chrono::milliseconds increment() const { return this->_increment; }

		void set_main_time(std::chrono::milliseconds);
		void set_byoyomi(std::chrono::milliseconds);
		void set_byoyomi_stones(int32_t);
		void set_increment(std::chrono::milliseconds);
		void set_main_time_left(std::chrono::milliseconds);
		void set_byoyomi_stones_left(int32_t);

		bool is_ticking() const { return this->_is_ticking; }
		bool timeout() const { return this->_timeout; }
		void set(std::chrono::milliseconds main_time_ms, std::chrono::milliseconds byoyomi_ms, int32_t byoyomi_stones, std::chrono::milliseconds inc_ms);
		void set_left(std::chrono::milliseconds main_time_left, int32_t byoyomi_stones_left);
		void start();
		void stop();
		void reset();
		void restart() { reset(); start(); }
		
		template<bool INCLUDE_BYOYOMI>
		std::chrono::milliseconds time_left();

		std::chrono::milliseconds byoyomi_left();

	private:
		std::chrono::milliseconds _main_time;
		std::chrono::milliseconds _byoyomi;	// 秒読み. メインの持ち時間が切れたときに, _byoyomi_stones手ごとに設けられる時間. 日本のゲームでよく用いられる.
		int32_t _byoyomi_stones;
		std::chrono::milliseconds _increment;	// フィッシャールールにおける1手ごとの持ち時間の加算分
		std::chrono::milliseconds _time_left;	// 持ち時間の残り + 秒読み
		int32_t _byoyomi_stones_left;
		bool _is_ticking;
		bool _timeout;
		std::chrono::high_resolution_clock::time_point check_point;
	};

	template std::chrono::milliseconds GameTimer::time_left<true>();
	template std::chrono::milliseconds GameTimer::time_left<false>();
}
