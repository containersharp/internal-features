using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;


[assembly: InternalsVisibleTo("SharpCR.Features.Tests")]
namespace SharpCR.Features.CloudStorage.Transport
{
    internal class FixedLengthStream : Stream
    {
        private readonly Stream _inputStream;
        private readonly long _initOffset;
        private readonly long _length;
        private long _pos = 0;

        public FixedLengthStream(Stream inputStream, long length)
        {
            _inputStream = inputStream;
            _initOffset = _inputStream.Position;
            _length = Math.Min(length, _inputStream.Length - _inputStream.Position);
        }
        
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var wantsToRead = Math.Min(count, Math.Min(_length, _inputStream.Length) - _pos);
            var bytesRead = _inputStream.Read(buffer, offset, (int)wantsToRead);
            _pos += wantsToRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin when OffsetValid(offset):
                {
                    _pos = offset;
                    _inputStream.Seek(offset + _initOffset, origin);
                    break;
                }
                case SeekOrigin.End:
                {
                    offset = _length - 1 - offset;
                    if(OffsetValid(offset))
                    {
                        _pos = offset;
                        _inputStream.Seek(offset + _initOffset, SeekOrigin.Begin);
                    }
                    break;
                }
                case SeekOrigin.Current:
                {
                    offset = _pos + offset;
                    if (OffsetValid(offset))
                    {
                        _pos = offset;
                        _inputStream.Seek(offset + _initOffset, SeekOrigin.Begin);
                    }
                    break;
                }
            }
            
            return _pos;
        }

        private bool OffsetValid(long offset)
        {
            return offset >= 0 && offset < _length;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead { get; } = true;
        public override bool CanWrite { get; } = false;

        public override bool CanSeek => true;
        public override long Length => _length;
        public override long Position
        {
            get => _pos;
            set
            {
                if (value < 0 || value >= _length)
                {
                    throw new IndexOutOfRangeException();
                }

                Seek(value, SeekOrigin.Begin);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _inputStream.Dispose();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            return _inputStream.DisposeAsync();
        }
    }
}