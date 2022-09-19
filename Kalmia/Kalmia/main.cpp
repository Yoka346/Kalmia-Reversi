#include <iostream>
#include "engine/random_mover.h"
#include "protocol/gtp.h"

#include "evaluate/feature.h"

#include "test/position_feature_init_test.h"

using namespace engine;
using namespace protocol;

int main()
{
	test::init_position_feature_test();
}