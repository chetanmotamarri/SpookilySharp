﻿// HashedStream.cs
//
// Author:
//     Jon Hanna <jon@hackcraft.net>
//
// © 2014–2017 Jon Hanna
//
// Licensed under the MIT license. See the LICENSE file in the repository root for more details.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SpookilySharp
{
    /// <summary>Wraps a stream, and maintains a hash of the bytes written to and from it.</summary>
    [Serializable]
    public class HashedStream : Stream
    {
        private readonly Stream _backing;
        private readonly SpookyHash _read;
        private readonly SpookyHash _written;

        /// <summary>Initialises a new instance of the <see cref="SpookilySharp.HashedStream"/> class.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="readSeed0">The first 64 bits of the seed for the hash of contents read.</param>
        /// <param name="readSeed1">The second 64 bits of the seed for the hash of contents read.</param>
        /// <param name="writeSeed0">The first 64 bits of the seed for the hash of contents written.</param>
        /// <param name="writeSeed1">The second 64 bits of the seed for the hash of contents written.</param>
        [CLSCompliant(false)]
        public HashedStream(Stream stream, ulong readSeed0, ulong readSeed1, ulong writeSeed0, ulong writeSeed1)
        {
            ExceptionHelper.CheckNotNull(stream);
            _backing = stream;
            _read = new SpookyHash(readSeed0, readSeed1);
            _written = new SpookyHash(writeSeed0, writeSeed1);
        }

        /// <summary>Initialises a new instance of the <see cref="SpookilySharp.HashedStream"/> class.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="readSeed0">The first 64 bits of the seed for the hash of contents read.</param>
        /// <param name="readSeed1">The second 64 bits of the seed for the hash of contents read.</param>
        /// <param name="writeSeed0">The first 64 bits of the seed for the hash of contents written.</param>
        /// <param name="writeSeed1">The second 64 bits of the seed for the hash of contents written.</param>
        public HashedStream(Stream stream, long readSeed0, long readSeed1, long writeSeed0, long writeSeed1)
            : this(stream, (ulong)readSeed0, (ulong)readSeed1, (ulong)writeSeed0, (ulong)writeSeed1)
        {
        }

        /// <summary>Initialises a new instance of the <see cref="SpookilySharp.HashedStream"/> class.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="seed0">The first 64 bits of the seed for both the hash of contents read and the hash of the
        /// contents written.</param>
        /// <param name="seed1">The second 64 bits of the seed for both the hash of contents read and the hash of the
        /// contents written.</param>
        [CLSCompliant(false)]
        public HashedStream(Stream stream, ulong seed0, ulong seed1)
            : this(stream, seed0, seed1, seed0, seed1)
        {
        }

        /// <summary>Initialises a new instance of the <see cref="SpookilySharp.HashedStream"/> class.</summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="seed0">The first 64 bits of the seed for both the hash of contents read and the hash of the
        /// contents written.</param>
        /// <param name="seed1">The second 64 bits of the seed for both the hash of contents read and the hash of the
        /// contents written.</param>
        public HashedStream(Stream stream, long seed0, long seed1)
            : this(stream, (ulong)seed0, (ulong)seed1)
        {
        }

        /// <summary>Initialises a new instance of the <see cref="SpookilySharp.HashedStream"/> class with a default
        /// seed.</summary>
        /// <param name="stream">The stream to read.</param>
        public HashedStream(Stream stream)
        {
            ExceptionHelper.CheckNotNull(stream);
            _backing = stream;
            _read = new SpookyHash();
            _written = new SpookyHash();
        }

        /// <inheritdoc/>
        public override bool CanRead => _backing.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => _backing.CanSeek;

        /// <inheritdoc/>
        public override bool CanTimeout => _backing.CanTimeout;

        /// <inheritdoc/>
        public override bool CanWrite => _backing.CanWrite;

        /// <inheritdoc/>
        public override void Close()
        {
            base.Close();
            _backing.Close();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _backing.Dispose();
            }
        }

        /// <inheritdoc/>
        public override void Flush() => _backing.Flush();

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) => _backing.FlushAsync(cancellationToken);

        /// <inheritdoc/>
        public override long Length => _backing.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => _backing.Position;
            set
            {
                _backing.Position = value;
                WasMoved = true;
            }
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = _backing.Read(buffer, offset, count);
            _read.Update(buffer, offset, count);
            return count;
        }

        /// <inheritdoc/>
        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count = await _backing.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _read.Update(buffer, offset, count);
            return count;
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            int ret = _backing.ReadByte();
            if (ret != -1)
            {
                _read.Update((byte)ret);
            }

            return ret;
        }

        /// <inheritdoc/>
        public override int ReadTimeout
        {
            get => _backing.ReadTimeout;
            set => _backing.ReadTimeout = value;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long ret = _backing.Seek(offset, origin);
            WasMoved = true;
            return ret;
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            _backing.SetLength(value);
            WasMoved = true;
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            _backing.Write(buffer, offset, count);
            _written.Update(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _backing.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _written.Update(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            _backing.WriteByte(value);
            _written.Update(value);
        }

        /// <inheritdoc/>
        public override int WriteTimeout
        {
            get => _backing.WriteTimeout;
            set => _backing.WriteTimeout = value;
        }

        /// <summary>Gets the current 128-bit hash of what has been written so far.</summary>
        /// <value>The hash, so far.</value>
        public HashCode128 WriteHash128 => _written.Final();

        /// <summary>Gets the current 64-bit hash of what has been written so far.</summary>
        /// <value>The hash, so far.</value>
        public long WriteHash64 => WriteHash128.Hash1;

        /// <summary>Gets the current 32-bit hash of what has been written so far.</summary>
        /// <value>The hash, so far.</value>
        public int WriteHash32 => (int)WriteHash128.Hash1;

        /// <summary>Gets the current 128-bit hash of what has been read so far.</summary>
        /// <value>The hash, so far.</value>
        public HashCode128 ReadHash128 => _read.Final();

        /// <summary>Gets the current 64-bit hash of what has been read so far.</summary>
        /// <value>The hash, so far.</value>
        public long ReadHash64 => ReadHash128.Hash1;

        /// <summary>Gets the current 32-bit hash of what has been read so far.</summary>
        /// <value>The hash, so far.</value>
        public int ReadHash32 => (int)ReadHash128.Hash1;

        /// <summary>Gets a value indicating whether there had been an operation that moved the point being read from or
        /// written to.</summary>
        /// <value><see langword="true"/> if was moved; otherwise, <see langword="false"/>.</value>
        /// <remarks>Operations such as <see cref="SetLength"/> or setting properties such as <see cref="Position"/>
        /// will mean that while the hashes will remain correct hashes of the values written and read, they may not
        /// correspond with e.g. the hash obtained by hashing the contents of a file the stream is backed by,
        /// etc.</remarks>
        public bool WasMoved { get; private set; }
    }
}
