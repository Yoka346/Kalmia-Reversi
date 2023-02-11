#pragma once

#include <iostream>
#include <vector>
#include <map>
#include <tuple>
#include <functional>
#include <future>
#include <atomic>
#include <optional>

#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/move.h"
#include "../reversi/position.h"
#include "engine_option.h"

namespace engine
{
	enum class EngineState
	{
		NOT_READY,
		READY,
		PLAYING,
		GAME_OVER
	};

	enum class EvalScoreType
	{
		WIN_RATE,
		DISC_DIFF,
		OTHER
	};

	/**
	* @struct
	* @brief �G���W���̎v�l���(�T���ǖʐ���o�ߎ���, �őP����� etc...)���܂Ƃ߂�\����.
	**/
	struct ThinkInfo
	{
		std::optional<std::chrono::milliseconds> ellapsed_ms = std::nullopt;
		std::optional<uint64_t> node_count = std::nullopt;
		std::optional<double> nps = std::nullopt;
		std::optional<int32_t> depth = std::nullopt;
		std::optional<int32_t> selected_depth = std::nullopt;
		std::optional<double> eval_score = std::nullopt;
		std::optional <std::vector <reversi::BoardCoordinate >> pv = std::nullopt;
	};

	/**
	* @struct
	* @brief MultiPV�̗v�f. �őP�����ƕ]�����܂Ƃ߂��\����.
	**/
	struct MultiPVItem
	{
		std::optional<uint64_t> node_count = std::nullopt;
		std::optional<double> eval_score = std::nullopt;	
		std::vector<reversi::BoardCoordinate> pv;
	};

	using MultiPV = std::vector<MultiPVItem>;

	/**
	* @class
	* @brief �v�l�G���W�����p�����钊�ۃN���X.
	**/
	class Engine
	{
	public:
		// �G���W����������𑗐M����Ƃ��ɌĂяo�����n���h��.
		std::function<void(const std::string&)> on_message_is_sent = [](const std::string&) { ; };

		// �G���W�����G���[������𑗐M����Ƃ��ɌĂяo�����n���h��.
		std::function<void(const std::string&)> on_err_message_is_sent = [](const std::string&) { ; };

		// �G���W�����v�l���𑗐M����Ƃ��ɌĂяo�����n���h��.
		std::function<void(const ThinkInfo&)> on_think_info_is_sent = [](const ThinkInfo&) { ; };

		// �G���W����multi PV�𑗐M����Ƃ��ɌĂяo�����n���h��.
		std::function<void(const MultiPV&)> on_multi_pv_is_sent = [](const MultiPV&) { ; };

		Engine(const std::string& name, const std::string& version, const std::string& author)
			: _name(name), _version(version), _author(author), _score_type(EvalScoreType::OTHER), _position(), position_history(), _is_thinking(false)
		{
			
		}

		EngineState state() const { return this->_state; }
		const std::string& name() const { return this->_name; }
		const std::string& version() const { return this->_version; }
		const std::string& author() const { return this->_author; }
		const reversi::Position& position() const { return this->_position; }
		const EvalScoreType score_type() const { return this->_score_type; }

		bool ready();
		void start_game();
		void end_game();
		void set_position(reversi::Position& pos);
		void clear_position();
		bool is_thinking() { return this->_is_thinking; }

		virtual void quit() { ; };
		virtual void set_main_time(reversi::DiscColor color, std::chrono::milliseconds main_time_ms) = 0;
		virtual void set_byoyomi(reversi::DiscColor color, std::chrono::milliseconds byoyomi) = 0;
		virtual void set_byoyomi_stones(reversi::DiscColor color, int32_t byoyomi_stones) = 0;
		virtual void set_time_inc(reversi::DiscColor color, std::chrono::milliseconds inc) = 0;

		/**
		* @fn
		* @brief �v�l�G���W���̋�����ݒ肷��.
		* @param (level) �v�l�G���W���̋������x��. ���x���̒l���Ӗ�������̂�, �v�l�G���W���ɂ���ĈقȂ�.
		**/
		virtual void set_level(int32_t level);

		/**
		* @fn
		* @brief �v�l�G���W�����ۗL���Ă���Ֆʂ̏��𒅎�move�ɂ���čX�V����.
		* @param (color) �f�B�X�N�̐F.
		* @param (move) ����(�f�B�X�N��z�u������W).
		* @return �Ֆʂ̍X�V�ɐ���������true.
		**/
		bool update_position(reversi::DiscColor color, reversi::BoardCoordinate move);

		/**
		* @fn
		* @brief �v�l�G���W�����ۗL���Ă���Ֆʂ�1��O�̏�Ԃɖ߂�.
		* @return �Ֆʂ̍X�V�ɐ���������true.
		**/
		bool undo_position();

		/**
		* @fn
		* @brief �v�l�G���W���̃I�v�V�����l��ݒ肷��.
		* @param (name) �I�v�V������.
		* @param (value) �I�v�V�����l.
		* @param (err_msg) �G���[���b�Z�[�W.
		* @return �I�v�V�����l�̐ݒ�ɐ���������true. ���s������G���[���b�Z�[�W��err_msg�Ɋi�[����false��Ԃ�.
		**/
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg);

		/**
		* @fn
		* @brief �v�l�G���W���̃I�v�V�����l��ݒ肷��.
		* @param (name) �I�v�V������.
		* @param (value) �I�v�V�����l.
		* @return �I�v�V�����l�̐ݒ�ɐ���������true.
		**/
		bool set_option(const std::string name, const std::string& value)
		{
			std::string dummy;
			return set_option(name, value, dummy);
		}

		/**
		* @fn
		* @brief �G���W���̃I�v�V�������ƃI�v�V�����l�̃^�v���̃��X�g���擾����.
		* @params (options) �G���W���̃I�v�V�������ƃI�v�V�����l�̃^�v���̃��X�g
		**/
		void get_options(EngineOptions& options);

		/**
		* @fn
		* @brief �őP��𐶐�����.
		* @param (ponder)
		* @return �őP��
		**/
		reversi::BoardCoordinate go(bool ponder);

		/**
		* @fn
		* @brief �v�l�G���W�����v�l���̏ꍇ�͂�����~����.
		* @param (timeout_ms) �^�C���A�E�g����(ms).
		* @return �v�l������ɏI��������true.
		* @note ���̊֐��͕ʃX���b�h�Ŏ��s����go�֐����I��������ۂɗp����.
		**/
		bool stop_thinking(std::chrono::milliseconds timeout);

		/**
		* @fn
		* @brief �]���l����蓾��ŏ��l��Ԃ�.
		* @return �]���l�̍ŏ��l.
		* @detail �Ⴆ��, �\�z�΍���]���l�Ƃ���ꍇ�� -64 ��Ԃ�, �\�z������]���l�Ƃ���ꍇ�� 0.0 ��Ԃ�.
		**/
		virtual double get_eval_score_min() { return 0.0; }

		/**
		* @fn
		* @brief �]���l����蓾��ő�l��Ԃ�.
		* @return �]���l�̍ő�l.
		* @detail �Ⴆ��, �\�z�΍���]���l�Ƃ���ꍇ�� 64 ��Ԃ�, �\�z������]���l�Ƃ���ꍇ�� 1.0 ��Ԃ�.
		**/
		virtual double get_eval_score_max() { return 0.0; }

	protected:
		EvalScoreType _score_type;
		std::map<std::string, EngineOption> options;

		bool stop_flag() { return this->_stop_flag.load(); }
		virtual bool on_ready() { return true; }
		virtual void on_start_game() { ; }
		virtual void on_end_game() { ; }
		virtual void on_cleared_position() { ; }
		virtual void on_position_was_set() { ; }
		virtual void on_undid_position() { ; }
		virtual void on_updated_position(reversi::BoardCoordinate move) { ; }
		virtual bool on_stop_thinking(std::chrono::milliseconds timeout) { return true; }
		virtual reversi::BoardCoordinate generate_move(bool ponder) = 0;

		/**
		* @fn
		* @brief ������ŕ\�����������G���W���̌Ăяo�����ɑ���.
		* @param (msg) ���镶����.
		**/
		void send_text_message(const std::string& msg);

		/**
		* @fn
		* @brief �G���[���b�Z�[�W���G���W���̌Ăяo�����ɑ���.
		* @param (msg) ���镶����.
		**/
		void send_err_message(const std::string& msg);

		/**
		* @fn
		* @brief �G���W���̎v�l�����Ăяo�����ɑ���.
		**/
		void send_think_info(ThinkInfo& think_info);

		/**
		* @fn
		* @brief Multi PV���G���W���̌Ăяo�����ɑ���.
		**/
		void send_multi_pv(MultiPV& multi_pv);

	private:
		EngineState _state = EngineState::NOT_READY;
		std::string _name;
		std::string _version;
		std::string _author;
		reversi::Position _position;
		std::vector<reversi::Position> position_history;
		std::atomic<bool> _stop_flag;
		std::atomic<bool> _is_thinking;
	};
}