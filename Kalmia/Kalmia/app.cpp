#include "app.h"

#include <string>
#include <algorithm>

#include "protocol/gtp.h"
#include "protocol/usi.h"
#include "protocol/nboard.h"
#include "engine/kalmia.h"
#include "engine/random_mover.h"

using namespace std;

using namespace protocol;
using namespace engine;

Application& Application::instance()
{
	static Application _instance;
	return _instance;
}

void Application::run(char* args[], size_t args_len)
{
	if (!apply_options(args, args_len))
		return;

	if (!this->protocol)
		this->protocol = make_unique<USI>();

	if (!this->engine)
		this->engine = make_unique<Kalmia>();

	this->protocol->mainloop(this->engine.get());
}

bool Application::apply_options(char* args[], size_t args_len)
{
	static const string OPTION_PREFIX = "--";

	size_t i = 0;
	while (i < args_len)
	{
		string arg = args[i];
		if (equal(OPTION_PREFIX.begin(), OPTION_PREFIX.end(), arg.begin()))
		{
			if (arg == "--protocol")
			{
				if (i == args_len - 1)
				{
					cout << "Error: Specify protocol name.";
					return false;
				}

				if (!set_protocol(args[++i]))
				{
					cout << "Error: \"" << args[i] << "\"" << " is invalid protocol.";
					return false;
				}
			}
			else if (arg == "--engine")
			{
				if (i == args_len - 1)
				{
					cout << "Error: Specify engine name.";
					return false;
				}

				if (!set_engine(args[++i]))
				{
					cout << "Error: \"" << args[i] << "\" is invalid engine.";
					return false;
				}
			}
			else
			{
				cout << "Error: \"" << arg << "\" is invalid option.";
				return false;
			}
		}
		i++;
	}
	return true;
}

bool Application::set_protocol(const string& protocol_name)
{
	string name = protocol_name;
	transform(name.begin(), name.end(), name.begin(), [](char c) { return tolower(c); });

	unique_ptr<IProtocol> prev;
	if (this->protocol)
		prev = move(this->protocol);

	if (name == "gtp")
		this->protocol = make_unique<GTP>();
	else if (name == "usi")
		this->protocol = make_unique<USI>();
	else if (name == "nboard")
		this->protocol = make_unique<NBoard>();
	else
		return false;

	return true;
}

bool Application::set_engine(const string& engine_name)
{
	string name = engine_name;
	transform(name.begin(), name.end(), name.begin(), [](char c) { return tolower(c); });

	unique_ptr<Engine> prev;
	if (this->engine)
		prev = move(this->engine);

	if (name == "kalmia")
		this->engine = make_unique<Kalmia>();
	else if (name == "random" || name == "random_mover")
		this->engine = make_unique<RandomMover>();
	else
		return false;
	return true;
}