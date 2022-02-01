using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kalmia.MCTS;
using Kalmia.Evaluation;
using Kalmia.GoTextProtocol;

namespace Kalmia.Engines
{
    /// <summary>
    /// Provides configuration of Kalmia Engine.
    /// </summary>
    public class KalmiaConfig
    {
        /// <summary>
        /// The number of search iteration count.
        /// </summary>
        public int SearchCount { get; set; }

        /// <summary>
        /// The time limit for thinking. The unit of time is millisecond.
        /// </summary>
        public int TimeLimit { get; set; }

        /// <summary>
        /// Whether extend thinking time or not when the thinking is not enough.
        /// </summary>
        public bool EnableTimeExtend { get; set; }

        /// <summary>
        /// The additional search count for extended think.
        /// </summary>
        public int ExtendedSearchCount { get; set; }

        /// <summary>
        /// The extended thinking time. The unit of time is millisecond.
        /// </summary>
        public int ExtenedTimeLimit { get; set; }

        /// <summary>
        /// Whether selects next move stochastically or not.
        /// </summary>
        public bool SelectMoveStochastically { get; set; }

        /// <summary>
        /// The softmax temperature when selecting next move stochastically.
        /// </summary>
        public float SoftmaxTemperture { get; set; }

        /// <summary>
        /// Whether reuses previous think result or not.
        /// </summary>
        public bool ReuseSubtree { get; set; }

        /// <summary>
        /// Whether continue thinking on opponent turn.
        /// </summary>
        public bool EnablePondering { get; set; }

        /// <summary>
        /// The path of value function parameter file.
        /// </summary>
        public string ValueFuncParamFile { get; set; }

        /// <summary>
        /// The search options.
        /// </summary>
        public UCTOptions TreeOptions { get; set; }
    }

    /// <summary>
    /// Provides reversi engine.
    /// </summary>
    public class KalmiaEngine : GTPEngine
    {
        const string _NAME = "Kalmia";
        const string _VERSION = "1.0";

        readonly int SEARCH_COUNT;
        readonly int TIME_LIMIT;
        readonly int TIME_EXTEND_ENABLED;
        readonly int EXTENDED_SEARCH_COUNT;
        readonly int EXTENDED_TIME_LIMIT;
        readonly bool SELECT_MOVE_STOCHASTICALLY;
        readonly float SOFTMAX_TEMPERATURE;
        readonly bool REUSE_SUBTREE;
        readonly bool PONDERING_ENABLED;
        readonly ValueFunction VALUE_FUNC;

        UCT tree;
    }
}
