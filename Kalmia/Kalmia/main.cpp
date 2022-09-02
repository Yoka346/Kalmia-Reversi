#include <iostream>
#include "engine/random_mover.h"
#include "protocol/gtp.h"

using namespace engine;
using namespace protocol;

int main()
{
	RandomMover engine;
	GTP gtp;
	gtp.mainloop(&engine, "gtp.log");
}