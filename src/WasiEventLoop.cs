using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using System;

namespace Wasi.Tls
{
    public static class WasiEventLoop
    {


        internal static Task Register(IPoll.Pollable pollable, CancellationToken cancellationToken)
        {
            var handle = pollable.Handle;
            pollable.Handle = 0;
            GC.SuppressFinalize(pollable);

            return CallRegisterWasiPollableHandle((Thread)null!, handle, true, cancellationToken);

            [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "RegisterWasiPollableHandle")]
            static extern Task CallRegisterWasiPollableHandle(Thread t, int handle, bool ownsPollable, CancellationToken cancellationToken);
        }
        
        public static void RunAsync(Func<Task> func)
        {
            throw new NotImplementedException("need to revisit how to dispatch");
        }

        public static T RunAsync<T>(Func<Task<T>> func)
        {
            throw new NotImplementedException("need to revisit how to dispatch");
        }
    }
}