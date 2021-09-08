using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalmia
{
    public static class StackExtension
    {
        public static Stack<T> Copy<T>(this Stack<T> stack, int size)
        {
            var copied = new Stack<T>(size);
            foreach (var n in stack.Reverse())
                copied.Push(n);
            return copied;
        }
    }
}
