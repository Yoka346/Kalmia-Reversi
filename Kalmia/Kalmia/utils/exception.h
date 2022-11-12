#pragma once
#include <exception>

namespace utils
{
	/**
	* @class
	* @brief �d�l��Ӑ}����Ă��Ȃ����삪�s��ꂽ�ۂɑ��o������O.
	* @note �W�����C�u�����Œ񋟂���Ă����O�N���X�ɑ����邽��, ���̃N���X���̂ݑS�ď������̃X�l�[�N�P�[�X��p����.
	**/
	class invalid_operation : public std::logic_error
	{
	public:
		invalid_operation(const std::string& message) : std::logic_error(message) { ; }
	};
}
