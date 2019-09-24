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
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Memory backed stream which spills to a temp file stream when the size limit
    /// is reached.
    /// </summary>
    /// <remarks>
    /// Thread Safety: Any instance of this class may only be called by a single thread
    /// at once, but subsequent calls may be performed by different threads. It is assumed
    /// that the BaseStreams make the same guarantees.
    /// </remarks>
    public sealed class SpillStream : Stream
    {
        private Stream _backStream;
        private Func<Stream>? _largeStreamFactory;
        private readonly long _limit;

        public SpillStream(long limit, Func<Stream>? largeStreamCreator = null, Stream? smallStream = null)
        {
            _limit = limit;
            _largeStreamFactory = largeStreamCreator ?? CreateTempFileStream;
            _backStream = smallStream ?? new MemoryStream();
            if (!_backStream.CanSeek || !_backStream.CanRead || !_backStream.CanWrite)
                throw new ArgumentException(Resources.TheStreamMustBeReadableWritableAndSeekable);
        }

        public SpillStream(long limit, Func<string> tempFileNameFactory, Stream? smallStream = null)
            : this(limit, () => CreateTempFileStream(tempFileNameFactory), smallStream)
        {
            if (tempFileNameFactory == null)
            {
                throw new ArgumentNullException(nameof(tempFileNameFactory));
            }
        }

        private static Stream CreateTempFileStream() => CreateTempFileStream(Path.GetTempFileName);

        private static Stream CreateTempFileStream(Func<string> tempFileNameFactory)
        {
            var fileName = tempFileNameFactory();
            return new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        }

        public bool IsOnLargeStream => _largeStreamFactory == null;

        public bool NeedUpgrade(long additionalBytes)
        {
            if (additionalBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(additionalBytes), Resources.AdditionalBytesMustNotBeNegative);

            if (IsOnLargeStream)
                return false; // already upgraded.

            if (_backStream.Position + additionalBytes > _limit)
                return true;

            return false;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanTimeout => _backStream.CanTimeout;

        public override bool CanWrite => true;

        public override long Length => _backStream.Length;

        public override long Position { get => _backStream.Position; set => _backStream.Position = value; }

        public override int ReadTimeout { get => _backStream.ReadTimeout; set => _backStream.ReadTimeout = value; }

        public override int WriteTimeout { get => _backStream.WriteTimeout; set => _backStream.WriteTimeout = value; }

        private void MigrateToLongFileIfNotYet()
        {
            if (_largeStreamFactory == null)
                return;
            Stream? newStream = null;
            Stream? oldStream = null;
            try
            {
                PrepareNewStream(_largeStreamFactory, out newStream, out oldStream, out long position);

                oldStream.CopyTo(newStream);

                newStream = PostpareNewStream(ref newStream, oldStream, position);
            }
            finally
            {
                // In case anything goes wrong, we dispose both streams as we're in an invalid state now.
                newStream?.Dispose();
                oldStream?.Dispose();
            }
        }

        private void PrepareNewStream(Func<Stream> largeStreamFactory, out Stream newStream, out Stream oldStream, out long Position)
        {
            newStream = largeStreamFactory!();
            if (!newStream.CanSeek || !newStream.CanRead || !newStream.CanWrite)
                throw new InvalidOperationException(Resources.TheStreamMustBeReadableWritableAndSeekable);

            if (_backStream.CanTimeout && newStream.CanTimeout)
            {
                newStream.ReadTimeout = _backStream.ReadTimeout;
                newStream.WriteTimeout = _backStream.WriteTimeout;
            }

            // From now on, we need to dispose both streams in case anything goes wrong, as we may
            // be in an invalid state (Position wrong etc...)
            oldStream = _backStream;

            Position = oldStream.Position;
            oldStream.Position = 0;
        }

        private Stream? PostpareNewStream(ref Stream newStream, Stream oldStream, long position)
        {
            newStream.Position = position;

            Trace.Assert(oldStream.Length == newStream.Length);

            // Everything worked well, we can transition now
            _backStream = newStream;
            _largeStreamFactory = null;
            return null;
        }

        private async ValueTask MigrateToLongFileIfNotYetAsync(CancellationToken token)
        {
            if (_largeStreamFactory == null)
                return;

            Stream? newStream = null;
            Stream? oldStream = null;
            try
            {
                PrepareNewStream(_largeStreamFactory, out newStream, out oldStream, out long position);

                await oldStream.CopyToAsync(newStream, 81920, token);

                newStream = PostpareNewStream(ref newStream, oldStream, position);
            }
            finally
            {
                // In case anything goes wrong, we dispose both streams as we're in an invalid state now.
#if NETCOREAPP3_0 || NETSTANDARD2_1
                if (newStream != null)
                    await newStream.DisposeAsync();
                if (oldStream != null)
                    await oldStream.DisposeAsync();
#else
                newStream?.Dispose();
                oldStream?.Dispose();
#endif
            }
        }

        public override string ToString()
        {
            return $"SpillStream({_limit}, {_backStream})";
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
        {
            return _backStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object? state)
        {
            // We need some fake type here, as TaskCompletionSource<void> does not exist.
            var tcs = new TaskCompletionSource<bool>(state);

            // Encapsulating WriteAsync here allows us to also handle the migration async,
            // if switching to the large stream is necessary.
            WriteAsync(buffer, offset, count).ContinueWith(t => tcs.FinishAsyncResult(t, callback));

            return tcs.Task;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _backStream.Dispose();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _backStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            ((Task<bool>)asyncResult).Wait();
        }

        public override void Flush()
        {
            _backStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _backStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _backStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _backStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return _backStream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _backStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (value > _limit)
                MigrateToLongFileIfNotYet();

            _backStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (NeedUpgrade(count))
                MigrateToLongFileIfNotYet();

            _backStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (NeedUpgrade(count))
                await MigrateToLongFileIfNotYetAsync(cancellationToken);

            await _backStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            if (NeedUpgrade(1))
                MigrateToLongFileIfNotYet();

            _backStream.WriteByte(value);
        }
    }
}
