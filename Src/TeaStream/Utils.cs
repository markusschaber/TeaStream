/* TeaStream Project - Stream Utilities to replicate, duplicate and process Streams in .NET (Core)
 * 
 * Copyright © 2019 Markus Schaber
 * 
 * Licensed under MIT License, see LICENSE.txt file in top level project directory. 
 */
namespace TeaStream
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class FinishedAsyncResult : IAsyncResult
    {
        internal FinishedAsyncResult(object state)
        {
            AsyncState = state;
        }

        public object AsyncState { get; }

        public WaitHandle AsyncWaitHandle => ((IAsyncResult)Task.CompletedTask).AsyncWaitHandle;

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }

    internal class FinishedAsyncResultWithInt : FinishedAsyncResult
    {
        internal FinishedAsyncResultWithInt(object state, int bytes)
            : base(state)
        {
            Bytes = bytes;
        }

        public int Bytes { get; }
    }
}
