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
	// ���̃e�X�g��init_position_feature_test���N���A���Ă��邱�Ƃ��O��.
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
				if (second)	// ���̂Ƃ��̓p�X�����Đ��ɂ��Ă���PositionFeature�̃R���X�g���N�^�ɓn���Ȃ���, update�̌��ʂƍ���Ȃ��Ȃ�.
					pos.pass();
				PositionFeature expected(pos);
				if (second)	// ������x�p�X�����Č��ɖ߂�.
					pos.pass();

				// �R���X�g���N�^�ɍX�V��̔Ֆʂ�n���ď���������������, �����X�V������������v���Ȃ����, update�֐��ɖ�肪����.
				assert(equal(pf.features.begin(), pf.features.end(), expected.features.begin()));	
			}
		}
	}
}

#endif