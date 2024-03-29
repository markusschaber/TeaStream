﻿/* TeaStream Project - Stream Utilities to replicate, duplicate and process Streams in .NET (Core)
 * 
 * Copyright © 2019 Markus Schaber
 * 
 * Licensed under MIT License, see LICENSE.txt file in top level project directory. 
 */
namespace TeaStream
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /**
     * <summary>A writable <see cref="Stream"/> which multiplies all written data into
     * several target streams, similar to the unix <code>tee</code> command.</summary>
     * <remarks>
     * Thread Safety: Any instance of this class may only be called by a single thread 
     * at once, but subsequent calls may be performed by different threads. It is assumed
     * that the BaseStreams make the same guarantees.
     * </remarks>
     */
    public sealed class TeaStream : Stream
    {
        private readonly Stream[] baseStreams;
        private readonly TeaFlags flags;

        /// <summary>
        /// Creates a new <see cref="TeaStream"/> instance.
        /// </summary>
        /// <param name="baseStreams">The streams we replicate our output. All of the must be writeable. <seealso cref="Stream.CanWrite"/></param>
        public TeaStream(params Stream[] baseStreams)
        {
            this.baseStreams = baseStreams ?? throw new ArgumentNullException(nameof(baseStreams));
            if (!baseStreams.All(s => s.CanWrite))
                throw new ArgumentException("All baseStreams must be writable!", nameof(baseStreams));
        }

        /// <summary>
        /// Creates a new <see cref="TeaStream"/> instance.
        /// </summary>
        /// <param name="flags">The <see cref="TeaFlags"/> to apply for this <see cref="TeaStream"/>.</param>
        /// <param name="baseStreams">The streams we replicate our output. All of the must be writeable. <seealso cref="Stream.CanWrite"/></param>
        public TeaStream(TeaFlags flags, params Stream[] baseStreams)
            : this(baseStreams)
        {
            this.flags = flags;
            if (ForceParallel && ForceSerial)
                throw new ArgumentException("Cannot apply both ForceParallel and ForceSerial flags!", nameof(flags));
            if (!Enum.IsDefined(typeof(TeaFlags), flags))
                throw new ArgumentException($"Invalid or unknown TeaFlag combination '{flags}'!", nameof(flags));
        }

        /// <summary>
        /// Force parallel operation even for old non-async methods.
        /// </summary>
        public bool ForceParallel => flags.HasFlag(TeaFlags.ForceParallel);

        /// <summary>
        /// Force serialized operation even for modern async methods.
        /// </summary>
        public bool ForceSerial => flags.HasFlag(TeaFlags.ForceSerial);

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanTimeout => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object? state) => throw new NotSupportedException();

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
        {
            // We need some fake type here, as TaskCompletionSource<void> does not exist.
            var tcs = new TaskCompletionSource<bool>(state);

            WriteAsync(buffer, offset, count).ContinueWith(t => tcs.FinishAsyncResult(t, callback));

            return tcs.Task;
        }
        
        public override int EndRead(IAsyncResult asyncResult) => throw new NotSupportedException();

        public override void EndWrite(IAsyncResult asyncResult)
        {
            ((Task<bool>)asyncResult).Wait();
        }

        public override void Flush()
        {
            if (ForceParallel)
            {
                Task.WhenAll(baseStreams.Select(s => Task.Run(s.Flush))).Wait();
            }
            else
            {
                var aggregator = new ExceptionAggregator();

                foreach (var stream in baseStreams)
                {
                    try
                    {
                        stream.Flush();
                    }
                    catch (Exception ex) when (!ForceSerial)
                    {
                        aggregator += ex;
                    }
                }
                aggregator.RaiseIfAggregated();
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (ForceSerial)
            {
                foreach (var stream in baseStreams)
                {
                    await stream.FlushAsync(cancellationToken);
                }
            }
            else if (ForceParallel)
            {
                await Task.WhenAll(baseStreams.Select(s => Task.Run(() => s.FlushAsync(cancellationToken), cancellationToken)));
            }
            else
            {
                await Task.WhenAll(baseStreams.Select(s => s.FlushAsync(cancellationToken)));
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override int ReadByte() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override string ToString()
        {
            return $"TeaStream({flags}, {baseStreams.Length})";
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (ForceParallel)
            {
                Task.WhenAll(baseStreams.Select(s => Task.Run(() => s.WriteAsync(buffer, offset, count)))).Wait();
            }
            else
            {
                var aggregator = new ExceptionAggregator();

                foreach (var stream in baseStreams)
                {
                    try
                    {
                        stream.Write(buffer, offset, count);
                    }
                    catch (Exception ex) when (!ForceSerial)
                    {
                        aggregator += ex;
                    }
                }
                aggregator.RaiseIfAggregated();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (ForceParallel)
            {
                await Task.WhenAll(baseStreams.Select(s => Task.Run(() => s.WriteAsync(buffer, offset, count, cancellationToken), cancellationToken)));
            }
            else if (ForceSerial)
            {
                var aggregator = new ExceptionAggregator();

                foreach (var stream in baseStreams)
                {
                    try
                    {
                        await stream.WriteAsync(buffer, offset, count, cancellationToken);
                    }
                    catch (Exception ex) 
                    {
                        aggregator += ex;
                    }
                }
                aggregator.RaiseIfAggregated();
            }
            else
            {
                await Task.WhenAll(baseStreams.Select(s => s.WriteAsync(buffer, offset, count, cancellationToken)));
            }
        }

        public override void WriteByte(byte value)
        {
            if (ForceParallel)
            {
                Task.WhenAll(baseStreams.Select(s => Task.Run(() => s.WriteByte(value)))).Wait();
            }
            else
            {
                var aggregator = new ExceptionAggregator();

                foreach (var stream in baseStreams)
                {
                    try
                    {
                        stream.WriteByte(value);
                    }
                    catch (Exception ex) when (!ForceSerial)
                    {
                        aggregator += ex;
                    }
                }
                aggregator.RaiseIfAggregated();
            }
        }

        protected override void Dispose(bool disposing)
        {
            // No special exception handling here, as Dispose() should never throw.
            if (disposing)
            {
                foreach (var stream in baseStreams)
                {
                    stream.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
