#pragma once
#include "feature.h"

namespace evaluation
{
	struct FeatureIdxOffsetTable
	{
		int t[FEATURE_NUM];

		constexpr FeatureIdxOffsetTable() : t()
		{

		}
	};

	/**
	 * @class
	 * @brief	Provides evaluation function. This evaluation function produces the estimated winning rate of game.
	 * @detail	This evaluation function calculates odds(winning_rate / (1 - winning_rate)) from the lenear sum of the weight of the appeared feature,
	 *			then inputs that estimated odds to standard sigmoid function to convert it to the winning rate.
	*/
	class EvalFunction
	{

	};
}