using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kalmia
{
    public class NormalRandom
    {
        readonly Random RAND;
        readonly float MU;
        readonly float SIGMA;

        public NormalRandom(float mu, float sigma) : this(mu, sigma, Random.Shared.Next()) { }

        public NormalRandom(float mu, float sigma, int seed)
        {
            this.RAND = new Random(seed);
            this.MU = mu;
            this.SIGMA = sigma;    
        }

        public float NextSingle()
        {
            var x = this.RAND.NextSingle();
            var y = this.RAND.NextSingle();
            return this.SIGMA * MathF.Sqrt(-2.0f * MathF.Log(x)) * MathF.Cos(2.0f * MathF.PI * y) + this.MU;
        }
    }
}
