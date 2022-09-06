#include <iostream>
#include "engine/random_mover.h"
#include "protocol/gtp.h"

#include "evaluate/feature.h"

#include "test/flip_test.h"

using namespace engine;
using namespace protocol;

int main()
{
	test::calc_flipped_discs_test();
}