#pragma once
#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/position.h"

namespace engine
{
	class Engine
	{
	protected:
		std::string _name;
		std::string _version;
		utils::GameTimer timer;
		reversi::Position _position;
		bool _is_thinking;

	public:
		Engine(std::string name, std::string version) : _name(name), _version(version), _position(), timer(), _is_thinking(false) { ; }
		inline const std::string& name() const { return this->_name; }
		inline const std::string& version() const { return this->_version; }
		inline const reversi::Position& position() const { return this->_position; }
		inline virtual void set_position(reversi::Position& pos) { this->_position = pos; }
		inline bool is_thinking() { return this->_is_thinking; }

		inline void set_time(std::chrono::milliseconds main_time, std::chrono::milliseconds byoyomi, std::chrono::milliseconds inc)
		{
			this->timer = GameTimer(main_time, byoyomi, inc);
		}

		/**
		* @fn
		* @brief �v�l�G���W�����ۗL���Ă���Ֆʂ̏��𒅎�move�ɂ���čX�V����.
		* @param (move) ����(�f�B�X�N��z�u������W).
		* @return �Ֆʂ̍X�V�ɐ���������true.
		**/
		inline virtual bool update_position(reversi::BoardCoordinate move) { return this->_position.update<true>(move); }

		/**
		* @fn
		* @brief �őP��𐶐�����.
		* @param (side_to_move) ���.
		* @param (move) �������ꂽ����̊i�[��.
		* @note ���̊֐���stop_thinking�֐����Ă΂ꂽ�ۂɒ����ɏI����, �b��̍őP���move�Ɋi�[���Ȃ���΂Ȃ�Ȃ�.
		* �܂�, �v�l����_is_thinking��true��, �v�l�I������false�ɂ��Ȃ���΂Ȃ�Ȃ�.
		**/
		virtual void generate_move(reversi::DiscColor side_to_move, reversi::BoardCoordinate& move) = 0;

		/**
		* @fn
		* @brief �v�l�G���W�����v�l���̏ꍇ�͂�����~����.
		* @param (timeout_ms) �^�C���A�E�g����(ms).
		* @return �v�l������ɏI��������true.
		* @note ���̊֐��͕ʃX���b�h�Ŏ��s����generate_move�֐����I��������ۂɗp����. 
		**/
		virtual bool stop_thinking(std::chrono::milliseconds timeout_ms) = 0;
	};
}