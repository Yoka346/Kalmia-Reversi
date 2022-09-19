#pragma once
#pragma inline_depth(255)

#include <cstdint>

#include "../config.h"

namespace utils
{
	/**
	* @struct
	* @brief ループを展開するテンプレート構造体.
	* @detail N回のループを, N個の同じ処理の羅列に変換する. 例えば,
	*
	* int32_t sum = 0;
	* LoopUnroller<3>()([&](const int32_t i){ sum += i + 1; });
	*
	* というコードを書いた場合, このコードは以下のように展開される.
	*
	* int32_t sum = 0;
	* sum += 1;
	* sum += 2;
	* sum += 3;
	*
	* ループを展開することで, ループカウンタの範囲チェックが消えるメリットや, コンパイラの最適化が効きやすくなるというメリットがある.
	*
	* @cite 将棋の思考エンジンAperyのUnroller構造体を元にしている.
	* https://github.com/HiraokaTakuya/apery/blob/d14471fc879062bfabbd181eaa91e90c7cc28a71/src/common.hpp#L249
	**/
	template<int32_t N>
	struct LoopUnroller
	{
		template <typename T> constexpr FORCE_INLINE void operator()(T t)
		{
			LoopUnroller<N - 1>()(t);
			t(N - 1);
		}
	};

	template<>
	struct LoopUnroller<0>
	{
		template <typename T> constexpr FORCE_INLINE void operator()(T t) { ; }
	};
}