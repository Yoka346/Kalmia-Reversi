#pragma once
#include <chrono>

namespace utils
{
	// ToDo: カナダ方式への対応.
	class GameTimer
	{
	private:
		std::chrono::milliseconds _main_time_ms;
		std::chrono::milliseconds _byoyomi_ms;	// 秒読み. メインの持ち時間が切れたときに, 1手ごとに設けられる時間. 日本のゲームでよく用いられる.
		std::chrono::milliseconds _increment_ms;	// フィッシャールールにおける1手ごとの持ち時間の加算分
		std::chrono::milliseconds _time_left_ms;	// 持ち時間の残り + 秒読み
		bool _is_ticking;
		bool _timeout;
		std::chrono::high_resolution_clock::time_point check_point;

	public:
		GameTimer() : GameTimer(std::chrono::milliseconds::zero(), std::chrono::milliseconds::zero(), std::chrono::milliseconds::zero()) { ; }
		GameTimer(std::chrono::milliseconds main_time_ms, std::chrono::milliseconds byoyomi_ms, std::chrono::milliseconds inc_ms)
			: _main_time_ms(main_time_ms), _byoyomi_ms(byoyomi_ms), _increment_ms(inc_ms), 
			  _time_left_ms(main_time_ms + byoyomi_ms), _is_ticking(false), _timeout(false) { ; }

		inline std::chrono::milliseconds main_time_ms() { return this->_main_time_ms; }
		inline std::chrono::milliseconds byoyomi_ms() { return this->_byoyomi_ms; }
		inline std::chrono::milliseconds increment_ms() { return this->_increment_ms; }
		inline bool is_ticking() { return this->_is_ticking; }
		inline bool timeout() { return this->_timeout; }
		void start();
		void stop();
		void reset();
		inline void restart() { reset(); start(); }
		std::chrono::milliseconds time_left_ms();
	};
}
