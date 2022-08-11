#pragma once

namespace utils
{
	/**
	* @class
	* @brief �N���X�̐ÓI�C�j�V�����C�U.
	* @tparam (T) �^�[�Q�b�g�̃N���X.
	* @tparam (func) �������������L�q�����֐��̃|�C���^.
	* @detail  �R���X�g���N�^����func���Ăяo������, T�N���X�̐錾�̌��StaticInitilizer<T, func>�^�̕ϐ���錾���邱�Ƃ�,
	* func�̏��������s�����.
	**/
	template<class T, void (*func)()>
	class StaticInitializer
	{
	public:
		StaticInitializer() { func(); };
	};
}