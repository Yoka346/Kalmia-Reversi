#pragma once
#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/position.h"
#include "engine_option.h"
#include <vector>
#include <tuple>
#include <functional>

namespace engine
{
	using EngineOptions = std::vector<std::pair<std::string, EngineOption>>;

	class Engine
	{
	protected:
		std::string _name;
		std::string _version;
		utils::GameTimer timer;
		reversi::Position _position;
		bool _is_thinking;

		/**
		* @fn
		* @brief ������ŕ\�����������G���W���̌Ăяo����(GUI��T�[�o�[�Ȃ�)�ɑ���.
		* @param (msg) ���镶����.
		* @detail �v�l���O�Ȃǂ̕�����ŕ\�����������G���W���̌Ăяo����(GUI��T�[�o�[�Ȃ�)�ɑ���.
		* �������������, �v���g�R���ɂ���ēK�؂Ȍ`�ŏo�͂����. �Ⴆ��, USI�v���g�R���ł����, info string�R�}���h�𗘗p��,
		* GTP�ł����, �G���[�o�͂ɏo�͂���.
		**/
		inline void send_text_message(std::string msg) { this->on_message_is_sent(msg); }

	public:
		// �G���W����������𑗐M����Ƃ��ɌĂяo�����n���h��.
		std::function<void(std::string&)> on_message_is_sent = [](std::string&) {};

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
		* @brief �v�l�G���W���̃I�v�V�����l��ݒ肷��.
		* @param (name) �I�v�V������.
		* @param (value) �I�v�V�����l.
		* @param (err_msg) �G���[���b�Z�[�W.
		* @return �I�v�V�����l�̐ݒ�ɐ���������true. ���s������G���[���b�Z�[�W��err_msg�Ɋi�[����false��Ԃ�.
		**/
		virtual bool set_option(const std::string& name, const std::string& value, std::string& err_msg) = 0;

		/**
		* @fn
		* @brief �v�l�G���W���̃I�v�V�����l��ݒ肷��.
		* @param (name) �I�v�V������.
		* @param (value) �I�v�V�����l.
		* @return �I�v�V�����l�̐ݒ�ɐ���������true.
		**/
		inline bool set_option(const std::string name, const std::string& value)
		{
			std::string dummy;
			return set_option(name, value, dummy);
		}

		/**
		* @fn
		* @brief �G���W���̃I�v�V�������ƃI�v�V�����l�̃^�v���̃��X�g���擾����.
		* @params (options) �G���W���̃I�v�V�������ƃI�v�V�����l�̃^�v���̃��X�g
		**/
		virtual void get_options(EngineOptions& options) = 0;

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