#ifdef _DEBUG

#include "position_feature_update_test.h"
#include "test_common.h"

#include <iostream>
#include <algorithm>
#include <cassert>

#include "../utils/random.h"
#include "../reversi/constant.h"
#include "../reversi/types.h"
#include "../reversi/position.h"
#include "../evaluate/feature.h"

using namespace std;

using namespace reversi;
using namespace evaluation;

namespace test
{
	// このテストはinit_position_feature_testをクリアしていることが前提.
	void update_position_feature_test()
	{
		static const int SAMPLE_NUM = 1000;

		Random rand;
		Array<Move, MAX_MOVE_NUM> moves;
		for (int32_t test_num = 0; test_num < SAMPLE_NUM; test_num++)
		{
			Position pos;
			PositionFeature pf(pos);
			DiscColor first_player_color = pos.side_to_move();
			while (!pos.is_gameover())
			{
				auto move_num = pos.get_next_moves(moves);
				if (!move_num)
				{
					pos.pass();
					pf.pass();
					continue;
				}

				auto& move = moves[rand.next(move_num)];
				pos.calc_flipped_discs(move);
				assert(pos.update(move.coord));
				pf.update(move);

				bool second = pos.side_to_move() != first_player_color;
				if (second)	// 後手のときはパスをして先手にしてからPositionFeatureのコンストラクタに渡さないと, updateの結果と合わなくなる.
					pos.pass();
				PositionFeature expected(pos);
				if (second)	// もう一度パスをして元に戻す.
					pos.pass();

				// コンストラクタに更新後の盤面を渡して初期化した特徴と, 差分更新した特徴が一致しなければ, update関数に問題がある.
				assert(equal(pf.features.begin(), pf.features.end(), expected.features.begin()));	
			}
		}
	}
}

#endif