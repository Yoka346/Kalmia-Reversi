#pragma once
#include <iostream>

#include "../engine/engine.h"

namespace protocol
{
	// 各プロトコルクラスが実装するインターフェース.
	class IProtocol
	{
	public:
		virtual void mainloop(engine::Engine* engine, const std::string& log_file_path) = 0;
		void mainloop(engine::Engine* engine) { std::string null_path = ""; mainloop(engine, null_path); }
	};
}