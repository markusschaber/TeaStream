/* TeaStream Project - Stream Utilities to replicate, duplicate and process Streams in .NET (Core)
 * 
 * Copyright © 2019 Markus Schaber
 * 
 * Licensed under MIT License, see LICENSE.txt file in top level project directory. 
 */
namespace TeaStream
{
    using global::TeaStream.Properties;

    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal class FinishedAsyncResult : IAsyncResult
    {
        internal FinishedAsyncResult(object? state)
        {
            AsyncState = state;
        }

        public object? AsyncState { get; }

        public WaitHandle AsyncWaitHandle => ((IAsyncResult)Task.CompletedTask).AsyncWaitHandle;

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }

    internal class FinishedAsyncResultWithInt : FinishedAsyncResult
    {
        internal FinishedAsyncResultWithInt(object? state, int bytes)
            : base(state)
        {
            Bytes = bytes;
        }

        public int Bytes { get; }
    }

    internal static class TcsHelper
    {
        internal static void FinishAsyncResult(this TaskCompletionSource<bool> tcs, Task task, AsyncCallback callback)
        {
            if (task.IsFaulted)
                tcs.TrySetException(task.Exception!.InnerException!);
            else if (task.IsCanceled)
                tcs.TrySetCanceled();
            else
                tcs.TrySetResult(true);

            callback?.Invoke(tcs.Task);
        }
    }

    internal readonly struct ExceptionAggregator
    {
        private readonly object? _buffer;

        internal ExceptionAggregator(Exception ex)
        {
            _buffer = ex ?? throw new ArgumentNullException(nameof(ex));
        }

        private ExceptionAggregator(Exception ex1, Exception ex2)
        {
            _buffer = new List<Exception> {
                ex1 ?? throw new ArgumentNullException(nameof(ex1))                ,
                ex2 ?? throw new ArgumentNullException(nameof(ex2))
            };
        }

        public static ExceptionAggregator operator +(ExceptionAggregator agg, Exception newEx)
        {
            if (agg._buffer is Exception oldEx)
                return new ExceptionAggregator(oldEx, newEx);

            if (agg._buffer is List<Exception> list)
            {
                list.Add(newEx);
                return agg;
            }

            return new ExceptionAggregator(newEx);
        }

        public void RaiseIfAggregated()
        {
            if (_buffer is Exception cex)
            {
                // rethrow the exception without mangling the stack trace
                ExceptionDispatchInfo.Capture(cex).Throw();
            }
            else if (_buffer is List<Exception> lex)
            {
                throw new AggregateException(Resources.SeveralCallsToBasestreamsFailed, lex);
            }
        }
    }
}
