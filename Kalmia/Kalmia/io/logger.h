#pragma once
#include <iostream>
#include <fstream>

namespace io
{
	/**
	* @class
	* @brief どこにも出力しないストリーム.
	* @detail コードをシンプルにするために利用する. 使用例としては, あるデータの出力先が指定されている場合とそうでない場合があるとき, このNullStreamを用いれば,
	* 特に面倒な場合分けは必要なくなる.
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
	* @brief ログの記録を受け持つクラス.
	* @detail 指定したファイルとストリームの両方に文字列を出力する.
	* 例えば, テキストファイルと標準出力を指定すれば, 標準出力にログを表示すると同時にファイルにもその内容を保存するという使い方ができる.
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
