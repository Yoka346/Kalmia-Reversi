#pragma once
#include <iostream>
#include <functional>

namespace engine
{
	class EngineOption;

	using EventHandler = std::function<void(const EngineOption&)>;

	/**
	* @class
	* @brief �G���W���̐ݒ荀��
	* @detail �G���W���̊e��ݒ��, �I�v�V������(string) -> EngineOptionItem�̃n�b�V���e�[�u���ŊǗ������.
	**/
	class EngineOption
	{

	private:
		inline static const EventHandler NULL_HANDLER = [](const EngineOption& sender) {};

		// �I�v�V�����̃f�t�H���g�l.
		std::string default_value;

		// �I�v�V�����̌��ݒl.
		std::string current_value;

		// �I�v�V�����l�̌^.
		// USI�v���g�R���ł�, GUI�Ƀ{�^���Őݒ肷�邩, �X�s���R���g���[���Őݒ肷�邩�Ȃǂ�,
		// ����`����K�v�����邽�߂ɕK�v. GTP�Ȃǂ̂��̑��̃v���g�R���ł͂قƂ�Ǖs�v.
		std::string type;

		// �I�v�V�����l�������ł���ۂ͈̔�. 
		int32_t min, max;

		size_t idx;

		// �V�����I�v�V�����l���ݒ肳���Ƃ��ɌĂяo�����n���h��.
		EventHandler on_value_change;

	public:
		EngineOption(bool value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(std::string& value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);

		// ������Z�q. ���ꂪ�Ăяo���ꂽ�^�C�~���O��on_value_changed�n���h�����Ăяo�����.
		EngineOption& operator=(const std::string& value);

		operator int() const;
		inline std::string to_string() { return this->current_value; }
	};
}