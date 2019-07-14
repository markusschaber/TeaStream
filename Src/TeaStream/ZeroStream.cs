/* TeaStream Project - Stream Utilities to replicate, duplicate and process Streams in .NET (Core)
 * 
 * Copyright © 2019 Markus Schaber
 * 
 * Licensed under MIT License, see LICENSE.txt file in top level project directory. 
 */
namespace TeaStream
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A <see cref="Stream"/> implementation which contains only zeroes, writes are ignored.
    /// </summary>
    /// <remarks>
    /// Thread Safety: Any instance of this class may only be called by a single thread
    /// at once, but subsequent calls may be performed by different threads.
    /// </remarks>
    public class ZeroStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanTimeout => false;

        public override bool CanWrite => true;

        public override long Length => long.MaxValue;

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Read(buffer, offset, count);

            var result = new FinishedAsyncResultWithInt(state, count);

            callback?.Invoke(result);

            return result;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Write(buffer, offset, count);

            var result = new FinishedAsyncResult(state);
            callback?.Invoke(result);

            return result;
        }

        /// <summary>
        /// Warning! This will write zeroes to the <paramref name="destination"/> <see cref="Stream"/>
        /// forever (or until cancelled using the <paramref name="cancellationToken"/>).
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="bufferSize"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                byte[] buffer = new byte[bufferSize];

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await destination.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                }
            }, cancellationToken);
        }

        private class FinishedAsyncResult : IAsyncResult
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

        private class FinishedAsyncResultWithInt : FinishedAsyncResult
        {
            internal FinishedAsyncResultWithInt(object state, int bytes)
                : base(state)
            {
                Bytes = bytes;
            }

            public int Bytes { get; }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return ((FinishedAsyncResultWithInt)asyncResult).Bytes;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            // Just verify the type.
            var _ = ((FinishedAsyncResult)asyncResult);
        }

        public override void Flush()
        { }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateArguments(buffer, offset, count);

            Array.Clear(buffer, offset, count);
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Read(buffer, offset, count);
            return Task.FromResult(count);
        }

        public override int ReadByte()
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return nameof(ZeroStream);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // We still check for valid arguments, as the contract for Stream dictates.
            ValidateArguments(buffer, offset, count);
        }

        // Validate the arguments, as required by the stream contract.
        private static void ValidateArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "offset negative");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "count negative");
            if ((long)offset + (long)count > (long)buffer.Length)
                throw new ArgumentException(nameof(count), "offset + count exceeds buffer");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateArguments(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override void WriteByte(byte value)
        { }
    }
}
