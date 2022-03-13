using System.Threading;
using System.Runtime.CompilerServices;

namespace Kalmia
{
    internal struct FastSpinLock
    {
        const int ENTER = 1;
        const int EXIT = 0;

        int syncFlag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter()
        {
            if (Interlocked.CompareExchange(ref this.syncFlag, ENTER, EXIT) == ENTER)
                Spin();
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit() => Volatile.Write(ref this.syncFlag, EXIT);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Spin()
        {
            var spinner = new SpinWait();
            spinner.SpinOnce();
            while (Interlocked.CompareExchange(ref this.syncFlag, ENTER, EXIT) == ENTER)
                spinner.SpinOnce();
        }
    }
}
