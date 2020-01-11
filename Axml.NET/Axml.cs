using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Axml.NET
{
    internal static class Axml
    {
        public const ushort ResNullType = 0x0000;
        public const ushort ResStringPoolType = 0x0001;
        public const ushort ResTableType = 0x0002;
        public const ushort ResXmlType = 0x0003;

        public const uint Utf8Flag = 1 << 8;
    }

    internal class AxmlParserException : Exception
    {
        public AxmlParserException(string message) : base(message)
        {
        }

        public AxmlParserException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    internal class AxmlParser : IDisposable
    {
        private readonly BinaryReader _reader;

        public readonly ulong Filesize;
        public readonly StringBlock SB;

        public AxmlParser(Stream stream)
        {
            _reader = new BinaryReader(stream, Encoding.Unicode);

            var size = _reader.BaseStream.Length;
            if (size < 8)
            {
                throw new AxmlParserException("Filesize is too small to be a valid AXML file");
            }

            if (size > 0xFFFFFFFF)
            {
                throw new AxmlParserException("Filesize is too large to be a valid AXML file");
            }

            ArscHeader axmlHeader;
            try
            {
                axmlHeader = new ArscHeader(ref _reader);
            }
            catch (Exception ex)
            {
                throw new AxmlParserException("Error parsing first resource header", ex);
            }

            Filesize = axmlHeader.Size;

            if (axmlHeader.HeaderSize != 8)
            {
                throw new AxmlParserException("Header size is not 8");
            }

            if (Filesize > (ulong) _reader.BaseStream.Length)
            {
                throw new AxmlParserException("Declared file size is larger than stream length");
            }

            if (Filesize < (ulong) _reader.BaseStream.Length)
            {
                throw new AxmlParserException("Declared file size is smaller than stream length");
            }

            if (axmlHeader.Type != Axml.ResXmlType)
            {
                throw new AxmlParserException("Resource type is not XML");
            }

            ArscHeader header;
            try
            {
                header = new ArscHeader(ref _reader, Axml.ResStringPoolType);
            }
            catch (Exception ex)
            {
                throw new AxmlParserException("Error parsing resource header of string pool", ex);
            }

            if (header.HeaderSize != 28)
            {
                throw new AxmlParserException("String chunk header size is not 28");
            }

            SB = new StringBlock(ref _reader, header);
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }

    internal class ArscHeaderException : Exception
    {
        public ArscHeaderException(string message) : base(message)
        {
        }
    }

    internal class ArscHeader
    {
        private const long RequiredSize = 8;

        public readonly long Start;

        public readonly ushort Type;
        public readonly ushort HeaderSize;
        public readonly ulong Size;

        public long End => Start + (long) Size;

        public ArscHeader(ref BinaryReader reader, ushort? expectedType = null)
        {
            Start = reader.BaseStream.Position;

            if (reader.BaseStream.Length < Start + RequiredSize)
            {
                throw new ArscHeaderException("Size is larger than stream length");
            }

            Type = reader.ReadUInt16();
            HeaderSize = reader.ReadUInt16();
            Size = reader.ReadUInt64();

            if (expectedType.HasValue && Type != expectedType)
            {
                throw new ArscHeaderException("Unexpected header type");
            }

            if (HeaderSize < RequiredSize)
            {
                throw new ArscHeaderException("Declared header size is smaller than required size");
            }

            if (Size < RequiredSize)
            {
                throw new ArscHeaderException("Declared chunk size is smaller than required size");
            }

            if (Size < HeaderSize)
            {
                throw new ArscHeaderException("Declared chunk size is smaller than header size");
            }
        }
    }

    internal class StringBlock
    {
        public readonly ArscHeader Header;

        public readonly uint StringCount;
        public readonly uint StyleCount;
        public readonly uint Flags;

        private readonly bool _isUtf8;

        public readonly uint StringPoolOffset;
        public readonly uint StylePoolOffset;

        private readonly uint[] _stringOffsets;
        private readonly uint[] _styleOffsets;

        private readonly byte[] _charBuff;
        private readonly uint[] _styles;

        public StringBlock(ref BinaryReader reader, ArscHeader header)
        {
            Header = header;
            StringCount = reader.ReadUInt32();
            StyleCount = reader.ReadUInt32();
            Flags = reader.ReadUInt32();

            _isUtf8 = (Flags & Axml.Utf8Flag) != 0;

            StringPoolOffset = reader.ReadUInt32();
            StylePoolOffset = reader.ReadUInt32();

            _stringOffsets = new uint[StringCount];
            for (var i = 0; i < StringCount; i++)
            {
                _stringOffsets[i] = reader.ReadUInt32();
            }

            _styleOffsets = new uint[StyleCount];
            for (var i = 0; i < StyleCount; i++)
            {
                _styleOffsets[i] = reader.ReadUInt32();
            }

            var size = Header.Size - StringPoolOffset;
            if (StylePoolOffset > 0 && StyleCount > 0)
            {
                size = StylePoolOffset - StringPoolOffset;
            }

            _charBuff = new byte[size];
            reader.Read(_charBuff, (int) reader.BaseStream.Position, (int) size);

            if (StylePoolOffset > 0 && StyleCount > 0)
            {
                size = Header.Size - StylePoolOffset;

                _styles = new uint[size / 4];
                for (var i = 0; i < (int) size / 4; i++)
                {
                    _styles[i] = reader.ReadUInt32();
                }
            }
        }

        public string GetString(int i)
        {
            if (i < 0 || _stringOffsets.Length < 1 || i > StringCount)
            {
                throw new IndexOutOfRangeException("No string present at provided index");
            }

            var offset = _stringOffsets[i];

            if (_isUtf8)
            {
                return Decode8(offset);
            }
            else
            {
                return Decode16(offset);
            }
        }

        private string Decode8(uint offset)
        {
        }

        private string Decode16(uint offset)
        {
        }
    }
}