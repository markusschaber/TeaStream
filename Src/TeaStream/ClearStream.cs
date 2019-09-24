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
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A <see cref="Stream"/> implementation filled with "clear" zeroes. All reads return
    /// zeroes, writes do not change the stream contents.
    /// </summary>
    /// <remarks>
    /// Thread Safety: Any instance of this class may only be called by a single thread
    /// at once, but subsequent calls may be performed by different threads.
    /// </remarks>
    public class ClearStream : Stream
    {
        private long _length;
        private long _position;

        /// <summary>
        /// If <see cref="Infinite"/> is <c>true</c>, the stream is infinitely long: The Position
        /// always stays <c>0</c>, the <see cref="Length"/> is always <see cref="Int64.MaxValue"/>,
        /// and it can always be written and read.
        /// </summary>
        public bool Infinite { get; set; } = false;

        /// <summary>
        /// If <see cref="CanExtend"/> is <c>true</c>, the stream can be extended by writing
        /// beyond it's end (like "real" <see cref="FileStream"/>s or <see cref="MemoryStream"/>s).
        /// (It still cannot be read beyond the end.)
        /// </summary>
        /// <remarks>This is ignored when the stream is <see cref="Infinite"/>.</remarks>
        public bool CanExtend { get; set; } = true;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanTimeout => false;

        public override bool CanWrite => true;

        public override long Length => Infinite ? long.MaxValue : _length;

        public long Remaining => (Length > Position) ? Length - Position : 0;

        public override long Position
        {
            get { return _position; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), Resources.CannotSeekBeforeBeginningOfFile);
                if (!Infinite)
                    _position = value;
            }
        }

        private int ChunkSize(int bufferSize)
        {
            var remaining = Remaining;
            if (remaining > bufferSize)
                return bufferSize;
            return (int)remaining;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
        {
            Read(buffer, offset, count);

            var result = new FinishedAsyncResultWithInt(state, count);

            callback?.Invoke(result);

            return result;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
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
        /// <returns>The task</returns>
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), Resources.BufferSizeMustBePositive);

            if (Position >= Length)
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                byte[] buffer = new byte[bufferSize];

                while (Remaining > 0)
                {
                    var chunkSize = ChunkSize(bufferSize);
                    cancellationToken.ThrowIfCancellationRequested();
                    await destination.WriteAsync(buffer, 0, chunkSize, cancellationToken);
                    Position += chunkSize;
                }
            }, cancellationToken);
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

            var chunkSize = ChunkSize(count);

            if (chunkSize > 0)
                Array.Clear(buffer, offset, chunkSize);

            if (!Infinite)
                Position += chunkSize;

            return chunkSize;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Read(buffer, offset, count);
            return Task.FromResult(count);
        }

        public override int ReadByte()
        {
            if (Remaining >= 1)
                return 0;
            return -1;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        if (offset < 0)
                            throw new IOException(Resources.InvalidSeekOffset);

                        if (!Infinite)
                            Position = offset;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        var newOffset = Position + offset;
                        if (newOffset < 0)
                            throw new IOException(Resources.InvalidSeekOffset);
                        Position = newOffset;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        var newOffset = Length + offset;
                        if (newOffset < 0)
                            throw new IOException(Resources.InvalidSeekOffset);
                        Position = newOffset;
                        break;
                    }
                default:
                    throw new ArgumentException(string.Format(Resources.InvalidSeekOriginOrigin, origin));
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), Resources.LengthCannotBeNegative);
            if (!Infinite)
                _length = value;
        }

        public override string ToString()
        {
            // This string should only be used for internal debug output and is intentionally not localized.
            return $"ZeroStream({Position}/{Length}{(CanExtend ? ", CanExtend" : "")} {(Infinite ? ", Infinite" : "")})";
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // We still check for valid arguments, as the contract for Stream dictates.
            ValidateArguments(buffer, offset, count);
            SimWrite(count);
        }

        private void SimWrite(int count)
        {
            if (Infinite)
                return; // We can write as much as we want.

            var maxLength = CanExtend ? long.MaxValue : Length;

            if (maxLength - count < Position && !CanExtend) // we cannot write beyond the stream limit!
                throw new IOException(Resources.StreamLenghtLimitExceeded);

            Position += count;
            _length = Math.Max(Length, Position);
        }

        // Validate the arguments, as required by the stream contract.
        private static void ValidateArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), Resources.OffsetNegative);
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), Resources.CountNegative);
            if ((long)offset + (long)count > (long)buffer.Length)
                throw new ArgumentException(nameof(count), Resources.OffsetCountExceedsBuffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // We still check for valid arguments, as the contract for Stream dictates.
            ValidateArguments(buffer, offset, count);
            SimWrite(count);

            return Task.CompletedTask;
        }

        public override void WriteByte(byte value)
            => SimWrite(1);
    }
}
