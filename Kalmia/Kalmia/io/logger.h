#pragma once
#include <iostream>
#include <fstream>

namespace io
{
	/**
	* @class
	* @brief �ǂ��ɂ��o�͂��Ȃ��X�g���[��.
	* @detail �R�[�h���V���v���ɂ��邽�߂ɗ��p����. �g�p��Ƃ��Ă�, ����f�[�^�̏o�͐悪�w�肳��Ă���ꍇ�Ƃ����łȂ��ꍇ������Ƃ�, ����NullStream��p�����,
	* ���ɖʓ|�ȏꍇ�����͕K�v�Ȃ��Ȃ�.
	**/
	class NullStream : public std::streambuf, public std::ostream
	{
		char buf_[128];
	protected:
		virtual int overflow(int c)
		{
			setp(buf_, buf_ + sizeof(buf_));
			return (c == eof()) ? '\0' : c;
		}

	public:
		NullStream() : buf_(), std::ostream(this) {}
	};

	/**
	* @class
	* @brief ���O�̋L�^���󂯎��N���X.
	* @detail �w�肵���t�@�C���ƃX�g���[���̗����ɕ�������o�͂���.
	* �Ⴆ��, �e�L�X�g�t�@�C���ƕW���o�͂��w�肷���, �W���o�͂Ƀ��O��\������Ɠ����Ƀt�@�C���ɂ����̓��e��ۑ�����Ƃ����g�������ł���.
	**/
	class Logger
	{
	private:
		NullStream* null_stream;

		std::ofstream ofs;
		std::ostream* sub_os;
		bool enabled_auto_flush;

	public:
		Logger(std::string& path) : ofs(path) { this->null_stream = new NullStream(); this->sub_os = dynamic_cast<std::ostream*>(this->null_stream); }
		Logger(std::string& path, std::ostream* sub_stream) : ofs(path), sub_os(sub_stream) { this->null_stream = nullptr; }
		~Logger() { if (this->null_stream) delete this->null_stream; }
		template<class T> Logger& operator <<(T t);
		inline void flush() { this->ofs.flush(); this->sub_os->flush(); }
		inline void enable_auto_flush() { this->enabled_auto_flush = true; }
		inline void disable_auto_flush() { this->enabled_auto_flush = false; }
	};
}
