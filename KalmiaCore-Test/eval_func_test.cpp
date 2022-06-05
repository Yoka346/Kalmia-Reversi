#include "eval_func_test.h"

using namespace std;
using namespace reversi;
using namespace evaluation;

constexpr const char* TEST_DATA_FILE_PATH = "test_data/eval_func_test_data.csv";
constexpr const char* TEST_EVAL_PARAM_PATH = "test_data/kalmia_eval_func.dat";

// ToDo: ‘ÎÌ•ÏŠ·‚ª³‚µ‚­À‘•‚³‚ê‚Ä‚¢‚é‚©Šm”F.
TEST(EvalFunc_Test, Eval_Test)
{
	constexpr float EPSILON = 1.0e-4f;

	EvalFunction eval_func(TEST_EVAL_PARAM_PATH);
	
	ifstream test_data(TEST_DATA_FILE_PATH);
	string line;
	string buffer;
	auto count = 0;
	while (getline(test_data, line))
	{
		stringstream ss(line);
		getline(ss, buffer, ',');
		auto p = stoull(buffer);
		getline(ss, buffer, ',');
		auto o = stoull(buffer);
		getline(ss, buffer, ',');
		auto score_expected = stof(buffer);

		Board board(DiscColor::BLACK, Bitboard(p, o));
		BoardFeature bf(board);
		float score_actual = eval_func.f(bf);

		ASSERT_TRUE(abs(score_expected - score_actual) <= EPSILON) << "test case: " << count << "\nexpected score is " << score_expected << " ,but actual score is " << score_actual << " .\n"
																   << "player's discs position is " << p << " .\n" << "opponent's discs positions is " << o << " .\n";
	}
}