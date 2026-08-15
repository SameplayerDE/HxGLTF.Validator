// Port of lib/src/data_access/image_decoder.dart

using System.Buffers.Binary;

namespace HxGLTF.Validator.Internal;

internal enum ImageCodec { JPEG, PNG, WebP, KTX2 }

internal static class ImageCodecMimeType
{
    private static readonly string[] MimeTypes = { "image/jpeg", "image/png", "image/webp", "image/ktx2" };

    public static string MimeType(this ImageCodec codec) => MimeTypes[(int)codec];
}

// Dart: _ColorPrimaries
internal enum ColorPrimaries { Unknown, sRGB, Custom }

// Dart: _ColorTransfer
internal enum ColorTransfer { Unknown, Linear, sRGB, Custom }

// Dart: Format
internal enum ImageFormat { Unknown, RGB, RGBA, Luminance, LuminanceAlpha }

internal sealed class ImageInfo
{
    public readonly string MimeType;
    public readonly int Bits;
    public readonly ImageFormat Format;
    public readonly int Width;
    public readonly int Height;
    public readonly ColorPrimaries ColorPrimaries;
    public readonly ColorTransfer ColorTransfer;
    public readonly bool HasNonSquarePixels;
    public readonly bool HasAnimation;

    public bool HasCustomColorInfo =>
        ColorPrimaries == ColorPrimaries.Custom || ColorTransfer == ColorTransfer.Custom;

    private ImageInfo(string mimeType, int bits, ImageFormat format, int width, int height,
        ColorPrimaries colorPrimaries = ColorPrimaries.Unknown,
        ColorTransfer colorTransfer = ColorTransfer.Unknown,
        bool hasNonSquarePixels = false,
        bool hasAnimation = false)
    {
        MimeType = mimeType;
        Bits = bits;
        Format = format;
        Width = width;
        Height = height;
        ColorPrimaries = colorPrimaries;
        ColorTransfer = colorTransfer;
        HasNonSquarePixels = hasNonSquarePixels;
        HasAnimation = hasAnimation;
    }

    /// <summary>Report string of the format ("rgb", "rgba", "luminance", "luminance-alpha") or null when unknown.</summary>
    public string? FormatName => Format switch
    {
        ImageFormat.RGB => "rgb",
        ImageFormat.RGBA => "rgba",
        ImageFormat.Luminance => "luminance",
        ImageFormat.LuminanceAlpha => "luminance-alpha",
        _ => null,
    };

    /// <summary>Report string of the primaries ("srgb", "custom") or null when unknown.</summary>
    public string? PrimariesName => ColorPrimaries switch
    {
        ColorPrimaries.sRGB => "srgb",
        ColorPrimaries.Custom => "custom",
        _ => null,
    };

    /// <summary>Report string of the transfer function ("linear", "srgb", "custom") or null when unknown.</summary>
    public string? TransferName => ColorTransfer switch
    {
        ColorTransfer.Linear => "linear",
        ColorTransfer.sRGB => "srgb",
        ColorTransfer.Custom => "custom",
        _ => null,
    };

    /// <summary>Dart ImageInfo.toMap().</summary>
    public ValidationImageInfo ToValidationImageInfo() => new()
    {
        Width = Width,
        Height = Height,
        Format = FormatName,
        Primaries = PrimariesName,
        Transfer = TransferName,
        Bits = Bits > 0 ? Bits : 0,
    };

    /// <summary>
    /// Dart ImageInfo.parseStreamAsync, synchronous over the whole image data.
    /// Throws UnsupportedImageFormatException, UnexpectedEndOfStreamException or InvalidDataFormatException.
    /// </summary>
    public static ImageInfo Parse(byte[] data) => Parse(new ReadOnlySpan<byte>(data));

    public static ImageInfo Parse(ReadOnlySpan<byte> data)
    {
        ImageInfoDecoder decoder;
        switch (DetectCodec(data))
        {
            case ImageCodec.JPEG:
                decoder = new JpegInfoDecoder();
                break;
            case ImageCodec.PNG:
                decoder = new PngInfoDecoder();
                break;
            case ImageCodec.WebP:
                decoder = new WebPInfoDecoder();
                break;
            default:
                throw new UnsupportedImageFormatException();
        }

        // Dart: decoder.add(data) followed by decoder.close() when the stream ends.
        var result = decoder.Add(data);
        if (result != null) return result;
        return ImageInfoDecoder.Close();
    }

    public static ImageCodec? DetectCodec(ReadOnlySpan<byte> firstChunk)
    {
        // Although only WebP detection requires 14 bytes,
        // valid images of other supported formats cannot be smaller than that.
        if (firstChunk.Length < 14)
        {
            return null;
        }

        var dword0 = BinaryPrimitives.ReadUInt32LittleEndian(firstChunk);

        // JPEG signature: FF D8 FF
        if ((dword0 & 0xFFFFFF) == 0xFFD8FF)
        {
            return ImageCodec.JPEG;
        }

        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        if (dword0 == 0x474E5089 &&
            BinaryPrimitives.ReadUInt32LittleEndian(firstChunk[4..]) == 0x0A1A0A0D)
        {
            return ImageCodec.PNG;
        }

        // WebP signature: 52 49 46 46 XX XX XX XX 57 45 42 50 56 50
        if (dword0 == 0x46464952 &&
            BinaryPrimitives.ReadUInt32LittleEndian(firstChunk[8..]) == 0x50424557 &&
            BinaryPrimitives.ReadUInt16LittleEndian(firstChunk[12..]) == 0x5056)
        {
            return ImageCodec.WebP;
        }

        // KTX2 signature: AB 4B 54 58 20 32 30 BB 0D 0A 1A 0A
        if (dword0 == 0x58544BAB &&
            BinaryPrimitives.ReadUInt32LittleEndian(firstChunk[4..]) == 0xBB303220 &&
            BinaryPrimitives.ReadUInt32LittleEndian(firstChunk[8..]) == 0x0A1A0A0D)
        {
            return ImageCodec.KTX2;
        }

        return null;
    }

    private abstract class ImageInfoDecoder
    {
        public abstract string MimeType { get; }

        /// <summary>Feeds data; returns the ImageInfo once it is known, null when more data is needed. Throws on invalid data.</summary>
        public abstract ImageInfo? Add(ReadOnlySpan<byte> data);

        /// <summary>Dart close(): the stream ended before the info was complete.</summary>
        public static ImageInfo Close() => throw new UnexpectedEndOfStreamException();
    }

    private sealed class JpegInfoDecoder : ImageInfoDecoder
    {
        public override string MimeType => "image/jpeg";

        private int _state;
        private int _type;
        private int _segmentLength;
        private int _segmentIndex;

        private byte[]? _sofBuffer;

        // States
        private const int START = 0x00;
        private const int LENGTH_START = 0x01;
        private const int LENGTH_END = 0x02;
        private const int SEGMENT = 0x03;

        private const int MARKER_START = 0xFF;

        private const int SOI = 0xD8; // Start of image
        private const int EOI = 0xD9; // End of image
        private const int TEM = 0x01; // Temporary AC use

        private const int RST = 0xD0; // Restart interval termination
        private const int RST_MASK = 0xF8;

        // Only Start-of-Frame markers contain dimensions:
        // C0-CF, except C4, C8, and CC; DE
        private const int SOF = 0xC0; // Start of frame
        private const int SOF_MASK = 0xF0;

        private const int DHP = 0xDE; // Define hierarchical progression

        private const int DHT = 0xC4; // Huffman table spec
        private const int SOF_EXT = 0xC8; // Reserved
        private const int DAC = 0xCC; // AC spec

        private static bool IsSOF(int marker) =>
            ((marker & SOF_MASK) == SOF &&
             marker != DHT &&
             marker != SOF_EXT &&
             marker != DAC) ||
            marker == DHP;

        private static bool HasSegment(int marker) => !(marker == TEM ||
                                                       (marker & RST_MASK) == RST ||
                                                       marker == SOI ||
                                                       marker == EOI ||
                                                       marker == MARKER_START);

        public override ImageInfo? Add(ReadOnlySpan<byte> data)
        {
            var index = 0;
            var availableDataLength = 0;

            while (index != data.Length)
            {
                var b = data[index];
                switch (_state)
                {
                    case START:
                        if (MARKER_START == b)
                        {
                            _state = MARKER_START;
                        }
                        else
                        {
                            throw new InvalidDataFormatException("Invalid start of file.");
                        }
                        break;

                    case MARKER_START:
                        if (HasSegment(b))
                        {
                            _state = LENGTH_START;
                            _type = b;
                            _segmentIndex = 0;
                            _segmentLength = 0;
                        }
                        break;

                    case LENGTH_START:
                        _segmentLength = b << 8;
                        _state = LENGTH_END;
                        break;

                    case LENGTH_END:
                        _segmentLength += b;
                        if (_segmentLength < 2)
                        {
                            throw new InvalidDataFormatException("Invalid JPEG marker segment length.");
                        }
                        if (IsSOF(_type))
                        {
                            _sofBuffer = new byte[_segmentLength - 2];
                        }
                        _state = SEGMENT;
                        break;

                    case SEGMENT:
                        availableDataLength = Math.Min(data.Length - index, _segmentLength - _segmentIndex - 2);
                        if (IsSOF(_type))
                        {
                            data.Slice(index, availableDataLength).CopyTo(_sofBuffer.AsSpan(_segmentIndex));
                            _segmentIndex += availableDataLength;

                            if (_segmentIndex == _segmentLength - 2)
                            {
                                return ParseSof();
                            }
                        }
                        else
                        {
                            _segmentIndex += availableDataLength;
                            if (_segmentIndex == _segmentLength - 2)
                            {
                                _state = MARKER_START;
                            }
                        }
                        index += availableDataLength;
                        continue;
                }
                index++;
            }
            return null;
        }

        private ImageInfo ParseSof()
        {
            var data = _sofBuffer!;
            var bits = data[0];
            var height = data[1] << 8 | data[2];
            var width = data[3] << 8 | data[4];

            ImageFormat format;
            if (data[5] == 3)
            {
                format = ImageFormat.RGB;
            }
            else if (data[5] == 1)
            {
                format = ImageFormat.Luminance;
            }
            else
            {
                throw new InvalidDataFormatException("Invalid number of JPEG color channels.");
            }

            return new ImageInfo(MimeType, bits, format, width, height);
        }
    }

    private sealed class PngInfoDecoder : ImageInfoDecoder
    {
        public override string MimeType => "image/png";

        private uint _chunkLength;
        private int _chunkLengthIndex;

        private uint _chunkType;
        private int _chunkTypeIndex;

        private int _chunkCrcIndex;

        private long _chunkDataIndex;

        private int _state;

        private bool _hasHeader;
        private bool _hasTrns;

        private ColorTransfer _transfer = ColorTransfer.Unknown;
        private ColorPrimaries _primaries = ColorPrimaries.Unknown;

        private bool _hasNonSquarePixels;

        private readonly byte[] _headerChunkBytes = new byte[13]; // IHDR length
        private readonly byte[] _chunkBytes = new byte[32]; // cHRM length

        private const int START = 0;
        private const int CHUNK_LENGTH = 1;
        private const int CHUNK_TYPE = 2;
        private const int CHUNK_DATA = 3;
        private const int CHUNK_CRC = 4;

        private const uint IHDR = 0x49484452;
        private const uint IDAT = 0x49444154;
        private const uint tRNS = 0x74524E53;
        private const uint cHRM = 0x6348524D;
        private const uint sRGB = 0x73524742;
        private const uint iCCP = 0x69434350;
        private const uint gAMA = 0x67414D41;
        private const uint pHYs = 0x70485973;

        private void Reset()
        {
            _chunkLength = 0;
            _chunkLengthIndex = 0;
            _chunkType = 0;
            _chunkTypeIndex = 0;
            _chunkDataIndex = 0;
            _chunkCrcIndex = 0;
        }

        public override ImageInfo? Add(ReadOnlySpan<byte> data)
        {
            const string wrongChunkLengthMessage = "Wrong chunk length.";

            var index = 0;
            long availableDataLength = 0;

            while (index != data.Length)
            {
                var b = data[index];

                switch (_state)
                {
                    case START:
                        index += 8; // skip PNG header, it should be already verified
                        _state = CHUNK_LENGTH;
                        continue;

                    case CHUNK_LENGTH:
                        _chunkLength = (_chunkLength << 8) | b;
                        _chunkLengthIndex++;
                        if (_chunkLengthIndex == 4)
                        {
                            _state = CHUNK_TYPE;
                        }
                        break;

                    case CHUNK_TYPE:
                        _chunkType = (_chunkType << 8) | b;
                        _chunkTypeIndex++;
                        if (_chunkTypeIndex == 4)
                        {
                            switch (_chunkType)
                            {
                                case IHDR:
                                    if (_chunkLength != 13)
                                    {
                                        throw new InvalidDataFormatException(wrongChunkLengthMessage);
                                    }
                                    _hasHeader = true;
                                    break;
                                case tRNS:
                                    _hasTrns = true;
                                    break;
                                case cHRM:
                                    if (_chunkLength != 32)
                                    {
                                        throw new InvalidDataFormatException(wrongChunkLengthMessage);
                                    }
                                    break;
                                case sRGB:
                                    if (_chunkLength != 1)
                                    {
                                        throw new InvalidDataFormatException(wrongChunkLengthMessage);
                                    }
                                    break;
                                case pHYs:
                                    if (_chunkLength != 9)
                                    {
                                        throw new InvalidDataFormatException(wrongChunkLengthMessage);
                                    }
                                    break;
                                case gAMA:
                                    if (_chunkLength != 4)
                                    {
                                        throw new InvalidDataFormatException(wrongChunkLengthMessage);
                                    }
                                    break;
                                case iCCP:
                                    _transfer = ColorTransfer.Custom;
                                    _primaries = ColorPrimaries.Custom;
                                    break;
                                case IDAT:
                                    return ParseIHDR();
                            }

                            if (_chunkLength == 0)
                            {
                                _state = CHUNK_CRC;
                            }
                            else
                            {
                                _state = CHUNK_DATA;
                            }
                        }
                        break;

                    case CHUNK_DATA:
                        availableDataLength = Math.Min(data.Length - index, _chunkLength - _chunkDataIndex);
                        switch (_chunkType)
                        {
                            case IHDR:
                                data.Slice(index, (int)availableDataLength).CopyTo(_headerChunkBytes.AsSpan((int)_chunkDataIndex));
                                _chunkDataIndex += availableDataLength;
                                break;

                            case cHRM:
                            case gAMA:
                            case pHYs:
                                data.Slice(index, (int)availableDataLength).CopyTo(_chunkBytes.AsSpan((int)_chunkDataIndex));
                                _chunkDataIndex += availableDataLength;
                                break;

                            case sRGB:
                                // The chunk contains one byte describing rendering intent
                                // 0 - perceptual
                                // 1 - relative colorimetric
                                // 2 - saturation-preserving
                                // 3 - absolute colorimetric
                                _transfer = ColorTransfer.sRGB;
                                _primaries = ColorPrimaries.sRGB;

                                _chunkDataIndex++;
                                break;

                            default:
                                _chunkDataIndex += availableDataLength;
                                break;
                        }

                        if (_chunkDataIndex == _chunkLength)
                        {
                            switch (_chunkType)
                            {
                                case cHRM:
                                    if (_primaries == ColorPrimaries.Unknown)
                                    {
                                        CheckChrm();
                                    }
                                    break;
                                case gAMA:
                                    if (_transfer == ColorTransfer.Unknown)
                                    {
                                        CheckGama();
                                    }
                                    break;
                                case pHYs:
                                    CheckPhys();
                                    break;
                            }

                            _state = CHUNK_CRC;
                        }

                        index += (int)availableDataLength;
                        continue;

                    case CHUNK_CRC:
                        _chunkCrcIndex++;
                        if (_chunkCrcIndex == 4)
                        {
                            Reset();
                            _state = CHUNK_LENGTH;
                        }
                        break;
                }
                index++;
            }
            return null;
        }

        private ImageInfo ParseIHDR()
        {
            if (!_hasHeader)
            {
                throw new InvalidDataFormatException("PNG header not found.");
            }

            var data = new ReadOnlySpan<byte>(_headerChunkBytes);

            var width = BinaryPrimitives.ReadUInt32BigEndian(data);
            var height = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
            var bits = data[8];

            var format = ImageFormat.Unknown;
            switch (data[9])
            {
                case 0: // Greyscale
                    format = _hasTrns ? ImageFormat.LuminanceAlpha : ImageFormat.Luminance;
                    break;
                case 2: // Truecolor
                case 3: // Indexed color
                    format = _hasTrns ? ImageFormat.RGBA : ImageFormat.RGB;
                    break;
                case 4: // Greyscale with alpha
                    format = ImageFormat.LuminanceAlpha;
                    break;
                case 6: // Truecolor with alpha
                    format = ImageFormat.RGBA;
                    break;
            }

            // No primaries defined, assume sRGB
            if (_primaries == ColorPrimaries.Unknown)
            {
                _primaries = ColorPrimaries.sRGB;
            }

            // No transfer function defined, assume sRGB
            if (_transfer == ColorTransfer.Unknown)
            {
                _transfer = ColorTransfer.sRGB;
            }

            // Dart: width/height are unsigned 32-bit ints; values above int.MaxValue are kept via unchecked cast.
            return new ImageInfo(MimeType, bits, format, unchecked((int)width), unchecked((int)height),
                colorPrimaries: _primaries,
                colorTransfer: _transfer,
                hasNonSquarePixels: _hasNonSquarePixels);
        }

        private void CheckGama()
        {
            // sRGB chunk overrides gAMA chunk
            if (_transfer == ColorTransfer.sRGB)
            {
                return;
            }

            // The value is encoded as a four-byte PNG unsigned integer,
            // representing gamma times 100000.

            // Default value is 45455 (1/2.2); linear (1) is also allowed
            switch (BinaryPrimitives.ReadUInt32BigEndian(_chunkBytes))
            {
                case 45455:
                    _transfer = ColorTransfer.sRGB;
                    break;
                case 100000:
                    _transfer = ColorTransfer.Linear;
                    break;
                default:
                    _transfer = ColorTransfer.Custom;
                    break;
            }
        }

        private void CheckPhys()
        {
            // Check that pixels are square
            var pixelsPerXUnit = BinaryPrimitives.ReadUInt32BigEndian(_chunkBytes);
            var pixelsPerYUnit = BinaryPrimitives.ReadUInt32BigEndian(_chunkBytes.AsSpan(4));

            if (pixelsPerXUnit != pixelsPerYUnit)
            {
                _hasNonSquarePixels = true;
            }
        }

        private void CheckChrm()
        {
            // sRGB chunk overrides cHRM chunk
            if (_primaries == ColorPrimaries.sRGB)
            {
                return;
            }

            // Each value is encoded as a four-byte PNG unsigned integer,
            // representing the x or y value times 100000.

            // Default values are

            // White point x 31270
            // White point y 32900
            // Red         x 64000
            // Red         y 33000
            // Green       x 30000
            // Green       y 60000
            // Blue        x 15000
            // Blue        y  6000

            var data = new ReadOnlySpan<byte>(_chunkBytes);

            if (BinaryPrimitives.ReadUInt32BigEndian(data) == 31270 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[4..]) == 32900 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[8..]) == 64000 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[12..]) == 33000 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[16..]) == 30000 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[20..]) == 60000 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[24..]) == 15000 &&
                BinaryPrimitives.ReadUInt32BigEndian(data[28..]) == 6000)
            {
                _primaries = ColorPrimaries.sRGB;
            }
            else
            {
                _primaries = ColorPrimaries.Custom;
            }
        }
    }

    private sealed class WebPInfoDecoder : ImageInfoDecoder
    {
        public override string MimeType => "image/webp";

        // Accumulate no more than 30 bytes of WebP header
        private readonly byte[] _buffer = new byte[30];
        private int _bufferIndex;

        private const uint RIFF = 0x52494646;
        private const uint WEBP = 0x57454250;
        private const uint VP8_ = 0x56503820;
        private const uint VP8L = 0x5650384C;
        private const uint VP8X = 0x56503858;

        public override ImageInfo? Add(ReadOnlySpan<byte> bytes)
        {
            var availableDataLength = Math.Min(bytes.Length, _buffer.Length - _bufferIndex);
            bytes[..availableDataLength].CopyTo(_buffer.AsSpan(_bufferIndex));
            _bufferIndex += availableDataLength;

            // We need 30 bytes for VP8 and VP8X, but only 25 for VP8L.
            if (_bufferIndex < 25 || _bufferIndex < 30 && _buffer[0xF] != 0x4C)
            {
                return null;
            }

            var byteData = new ReadOnlySpan<byte>(_buffer);

            // RIFF size WEBP
            if (BinaryPrimitives.ReadUInt32BigEndian(byteData) != RIFF ||
                BinaryPrimitives.ReadUInt32BigEndian(byteData[8..]) != WEBP)
            {
                throw new InvalidDataFormatException("Wrong WebP header.");
            }

            var format = ImageFormat.Unknown;
            var width = -1;
            var height = -1;
            var hasCustomColorInfo = false;
            var hasAnimation = false;

            // 4 bytes with chunk type followed by its size
            var type = BinaryPrimitives.ReadUInt32BigEndian(byteData[12..]);
            switch (type)
            {
                case VP8_:
                    // Skipping first 6 bytes of VP8 bitstream

                    // No alpha channel
                    format = ImageFormat.RGB;

                    // 14 bits of width
                    width = BinaryPrimitives.ReadUInt16LittleEndian(byteData[26..]) & 0x3FFF;

                    // 14 bits of height
                    height = BinaryPrimitives.ReadUInt16LittleEndian(byteData[28..]) & 0x3FFF;
                    break;
                case VP8L:
                    // Skipping the first byte of VP8L bitstream

                    // 1-based, 14 bits of width, LSB-packed
                    width = 1;
                    width += _buffer[21] | ((_buffer[22] & 0x3F) << 8);

                    // 1-based, 14 bits of height, LSB-packed
                    height = 1;
                    height += (_buffer[22] >> 6) |
                              (_buffer[23] << 2) |
                              ((_buffer[24] & 0xF) << 10);

                    // alpha_is_used
                    format = (_buffer[24] & 0x10) == 0x10 ? ImageFormat.RGBA : ImageFormat.RGB;
                    break;
                case VP8X:
                    // Used features byte
                    var features = _buffer[20];
                    hasAnimation = (features & 2) == 2;
                    hasCustomColorInfo = (features & 0x20) == 0x20;
                    format = (features & 0x10) == 0x10 ? ImageFormat.RGBA : ImageFormat.RGB;

                    // 1-based, 24 bits of width
                    width = (_buffer[24] | (_buffer[25] << 8) | (_buffer[26] << 16)) + 1;

                    // 1-based, 24 bits of height
                    height = (_buffer[27] | (_buffer[28] << 8) | (_buffer[29] << 16)) + 1;
                    break;
                default:
                    throw new InvalidDataFormatException("Wrong WebP header.");
            }
            return new ImageInfo(MimeType, 8, format, width, height,
                colorTransfer: hasCustomColorInfo ? ColorTransfer.Custom : ColorTransfer.sRGB,
                colorPrimaries: hasCustomColorInfo ? ColorPrimaries.Custom : ColorPrimaries.sRGB,
                hasAnimation: hasAnimation);
        }
    }
}

internal sealed class UnsupportedImageFormatException : Exception
{
    public UnsupportedImageFormatException() : base("Unsupported image format.") { }
}

internal sealed class UnexpectedEndOfStreamException : Exception
{
    public UnexpectedEndOfStreamException() : base("Unexpected end of stream.") { }
}

internal sealed class InvalidDataFormatException : Exception
{
    public InvalidDataFormatException(string message) : base(message) { }

    /// <summary>Dart toString() returns only the message.</summary>
    public override string ToString() => Message;
}
