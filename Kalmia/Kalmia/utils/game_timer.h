#pragma once
#include <chrono>

namespace utils
{
	class GameTimer
	{
	private:
		std::chrono::milliseconds _main_time_ms;
		std::chrono::milliseconds _byoyomi_ms;	// �b�ǂ�. ���C���̎������Ԃ��؂ꂽ�Ƃ���, _byoyomi_stones�育�Ƃɐ݂����鎞��. ���{�̃Q�[���ł悭�p������.
		int32_t _byoyomi_stones;	 
		std::chrono::milliseconds _increment_ms;	// �t�B�b�V���[���[���ɂ�����1�育�Ƃ̎������Ԃ̉��Z��
		std::chrono::milliseconds _time_left_ms;	// �������Ԃ̎c�� + �b�ǂ�
		int32_t _byoyomi_stones_left;
		bool _is_ticking;
		bool _timeout;
		std::chrono::high_resolution_clock::time_point check_point;

	public:
		GameTimer() : GameTimer(std::chrono::milliseconds::zero(), std::chrono::milliseconds::zero(), 1, std::chrono::milliseconds::zero()) { ; }
		GameTimer(std::chrono::milliseconds main_time_ms, std::chrono::milliseconds byoyomi_ms, int32_t byoyomi_stones, std::chrono::milliseconds inc_ms)
			: _main_time_ms(main_time_ms), _byoyomi_ms(byoyomi_ms), _byoyomi_stones(byoyomi_stones), _increment_ms(inc_ms),
			  _time_left_ms(main_time_ms + byoyomi_ms), _byoyomi_stones_left(byoyomi_stones), _is_ticking(false), _timeout(false) { ; }

		std::chrono::milliseconds main_time_ms() const { return this->_main_time_ms; }
		std::chrono::milliseconds byoyomi_ms() const { return this->_byoyomi_ms; }
		int32_t byoyomi_stones() const { return this->_byoyomi_stones; }
		int32_t byoyomi_stones_left() const { return this->_byoyomi_stones_left; }
		std::chrono::milliseconds increment_ms() const { return this->_increment_ms; }
		bool is_ticking() const { return this->_is_ticking; }
		bool timeout() const { return this->_timeout; }
		void set(std::chrono::milliseconds main_time_ms, std::chrono::milliseconds byoyomi_ms, int32_t byoyomi_stones, std::chrono::milliseconds inc_ms);
		void set_left(std::chrono::milliseconds main_time_left, int32_t byoyomi_stones_left);
		void start();
		void stop();
		void reset();
		void restart() { reset(); start(); }
		
		template<bool INCLUDE_BYOYOMI>
		std::chrono::milliseconds time_left_ms();

		std::chrono::milliseconds byoyomi_left_ms();
	};

	template std::chrono::milliseconds GameTimer::time_left_ms<true>();
	template std::chrono::milliseconds GameTimer::time_left_ms<false>();
}
