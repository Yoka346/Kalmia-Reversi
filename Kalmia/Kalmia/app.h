#pragma once
#include <memory>
#include <map>
#include <functional>

#include "protocol/protocol.h"
#include "engine/engine.h"

/**
* @class
* @brief アプリケーション部.
* @detail 当然ながら1つのプロセスにアプリケーションは1つなので, シングルトンクラス.
**/
class Application
{
public:
	static Application& instance();

	Application(Application&) = delete;
	void operator=(const Application&) = delete;

	void run(char* args[], size_t args_len); 

protected:
	Application() : protocol(), engine() { ; }

private:
	std::unique_ptr<protocol::IProtocol> protocol;
	std::unique_ptr<engine::Engine> engine;
	bool apply_options(char* args[], size_t args_len);

	bool set_protocol(const std::string& protocol_name);
	bool set_engine(const std::string& engine_name);
};

