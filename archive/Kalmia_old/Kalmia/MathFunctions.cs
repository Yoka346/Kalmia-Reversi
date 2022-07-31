using System;

namespace Kalmia
{
    public static class MathFunctions
    {
        public static float StdSigmoid(float x)
        {
            return 1.0f / (1.0f + MathF.Exp(-x));
        }

        public static void Softmax(float[] x, float[] y)
        {
            Softmax(x.AsSpan(), y.AsSpan());
        }

        public static void Softmax(Span<float> x, Span<float> y)
        {
            var sum = 0.0f;
            for (var i = 0; i < x.Length; i++)
                sum += y[i] = MathF.Exp(x[i]);
            for (var i = 0; i < y.Length; i++)
                y[i] = y[i] / sum;
        }

        public static float BinaryCrossEntropy(float y, float t)
        {
            const float EPSILON = 1.0e-6f;
            return -(t * MathF.Log(y + EPSILON) + (1.0f - t) * MathF.Log(1.0f - y + EPSILON));
        }

        public static float OneHotCrossEntropy(float[] y, int i)
        {
            return OneHotCrossEntropy(y.AsSpan(), i);
        }

        public static float OneHotCrossEntropy(Span<float> y, int i)
        {
            const float EPSILON = 1.0e-7f;
            return -MathF.Log(y[i] + EPSILON);
        }
    }
}
