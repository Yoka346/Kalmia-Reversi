using System;
using System.Collections.Generic;
using System.Text;

namespace Kalmia
{
    public static class DllInfo
    {
#if DEBUG
        public static string DllVersion { get; } = "Debug Version 0.0.8";
#elif RELEASE
        public static string DllVersion { get; } = "Release Version 0.0.8";
#endif

        public static void PrintDllVersion()
        {
            Console.WriteLine(DllVersion);
        }
    }
}
