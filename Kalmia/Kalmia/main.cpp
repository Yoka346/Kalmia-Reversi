#include <iostream>
#include <memory>
#include "engine/kalmia.h"
#include "protocol/gtp.h"
#include "protocol/usi.h"

#include "evaluate/feature.h"

#include "test/position_eval_test.h"

using namespace std;

using namespace engine;
using namespace protocol;

int main()
{
	static const string PARAM_PATH = "../test_data/value_func_weight_for_test.bin";
	unique_ptr<Kalmia> kalmia(new Kalmia(PARAM_PATH, "kalmia.log"));

	USI usi;
	usi.mainloop(kalmia.get(), "usi.log");
}