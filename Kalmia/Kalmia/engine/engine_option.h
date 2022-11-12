#pragma once
#include <iostream>
#include <string>
#include <functional>

namespace engine
{
	class EngineOption;

	using EventHandler = std::function<void(EngineOption&, std::string&)>;
	using EngineOptions = std::vector<std::pair<std::string, EngineOption>>;

	std::string engine_options_to_string(EngineOptions options);

	/**
	* @class
	* @brief �G���W���̐ݒ荀��
	* @detail �G���W���̊e��ݒ��, �I�v�V������(string) -> EngineOptionItem�̃n�b�V���e�[�u���ŊǗ������.
	**/
	class EngineOption
	{
	public:
		EngineOption() { ; }
		EngineOption(bool value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(const char* value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);

		const std::string& default_value() const { return this->_default_value; }
		const std::string& current_value() const { return this->_current_value; }
		const std::string& type() const { return this->_type; }
		int32_t min() const { return this->_min; }
		int32_t max() const { return this->_max; }
		size_t idx() const { return this->_idx; }
		const std::string& last_err_msg() const { return this->_last_err_msg; }

		// ������Z�q. ���ꂪ�Ăяo���ꂽ�^�C�~���O��on_value_changed�n���h�����Ăяo�����.
		EngineOption& operator=(const std::string& value);

		operator int32_t() const;

	private:
		inline static const EventHandler NULL_HANDLER = [](EngineOption& sender, std::string& err_msg) {};

		// �I�v�V�����̃f�t�H���g�l.
		std::string _default_value;

		// �I�v�V�����̌��ݒl.
		std::string _current_value;

		// �I�v�V�����l�̌^.
		// USI�v���g�R���ł�, GUI�Ƀ{�^���Őݒ肷�邩, �X�s���R���g���[���Őݒ肷�邩�Ȃǂ�,
		// ����`����K�v�����邽�߂ɕK�v. GTP�Ȃǂ̂��̑��̃v���g�R���ł͂قƂ�Ǖs�v.
		std::string _type;

		// �I�v�V�����l�������ł���ۂ͈̔�. 
		int32_t _min, _max;

		size_t _idx;

		// �V�����I�v�V�����l���ݒ肳���Ƃ��ɌĂяo�����n���h��.
		EventHandler on_value_change;
		std::string _last_err_msg;
		const std::string& to_string() const { return this->_current_value; }
	};
}