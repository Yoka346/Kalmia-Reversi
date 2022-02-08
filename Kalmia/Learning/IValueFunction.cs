using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Kalmia.Evaluation;

namespace Kalmia.Evaluation
{
    public interface IValueFunction
    {
        float F(BoardFeature board);
    }
}
