// Port of lib/src/glb_reader.dart

using System.Buffers.Binary;

namespace HxGLTF.Validator.Internal;

/// <summary>
/// Byte-level GLB container reader. The Dart reader consumes an async byte stream; this port processes the
/// complete byte array as a single data event followed by the done event, which reproduces the issue order of
/// the reference implementation for files delivered in one chunk: container issues found while scanning the
/// data first, then the JSON chunk (parse issues and Gltf.FromMap), then the end-of-stream checks.
/// </summary>
internal sealed class GlbReader
{
    private const int _HEADER_LENGTH = 12;

    private const int _CHUNK_HEADER_LENGTH = 8;
    private const uint _GLB_VERSION = 2;

    private const uint _GLTF_MAGIC = 0x46546C67;

    // States
    private const uint _HEADER = 0;
    private const uint _CHUNK_HEADER = 1;

    private const uint _CHUNK_JSON = 0x4E4F534A;
    private const uint _CHUNK_BIN = 0x004E4942;
    private const uint _CHUNK_UNKNOWN = 0xFFFFFFFF;

    public const string MimeType = "model/gltf-binary";

    private readonly byte[] _header = new byte[_HEADER_LENGTH];

    private readonly byte[] _data;
    private readonly Context _context;

    private uint _state = _HEADER;
    private int _localOffset;

    private long _offset;
    private long _totalLength;

    private int _chunkNumber;
    private long _chunkLength;
    private uint _chunkType;

    private bool _hasJsonChunk;
    // Dart: _jsonReader / _jsonStreamController; the JSON chunk bytes are collected and parsed after the data event.
    private bool _hasJsonReader;
    private int _jsonStart;
    private int _jsonLength;
    private bool _jsonStreamClosed;
    private bool _jsonProcessed;
    private Gltf? _jsonGltf;

    private bool _hasBinChunk;
    private byte[]? _binaryBuffer;

    // Completion state (Dart: _completer)
    private bool _completed;
    private bool _completeWithJsonResult;
    private GltfReaderResult? _result;

    private GlbReader(byte[] data, Context context)
    {
        _data = data;
        _context = context;
        _context.SetGlb();
    }

    public Context Context => _context;

    /// <summary>Dart <c>GlbReader(stream, context).read()</c>.</summary>
    public static GltfReaderResult Read(byte[] data, Context context)
    {
        var reader = new GlbReader(data, context);
        return reader.ReadInternal();
    }

    private GltfReaderResult ReadInternal()
    {
        try
        {
            OnData(_data);
        }
        catch (IssuesLimitExceededException)
        {
            Abort();
        }

        // Dart: the JSON reader receives its stream events (microtasks) between the data and done events.
        ProcessJson();

        if (!_completed)
        {
            OnDone();
        }
        else if (_completeWithJsonResult)
        {
            _result = new GltfReaderResult(MimeType, _jsonGltf, _binaryBuffer);
        }

        return _result ?? new GltfReaderResult(MimeType, null, _binaryBuffer);
    }

    private void Abort()
    {
        if (!_completed)
        {
            _completed = true;
            _result = new GltfReaderResult(MimeType, null, _binaryBuffer);
        }
    }

    private void CompleteWithJsonResult()
    {
        if (!_completed)
        {
            _completed = true;
            _completeWithJsonResult = true;
        }
    }

    private void ProcessJson()
    {
        if (!_hasJsonReader || _jsonProcessed)
        {
            return;
        }
        _jsonProcessed = true;

        bool abortedBeforeClose;
        try
        {
            _jsonGltf = GltfJsonReader.Process(new ReadOnlySpan<byte>(_data, _jsonStart, _jsonLength), _jsonStreamClosed, _context, out abortedBeforeClose);
        }
        catch (IssuesLimitExceededException)
        {
            _jsonGltf = null;
            abortedBeforeClose = !_jsonStreamClosed;
        }

        // Dart: the JSON reader cancels its subscription; when its stream is still open this cancels the GLB
        // subscription as well (no done event follows).
        if (abortedBeforeClose)
        {
            Abort();
        }
    }

    private static string GetChunkString(uint type) => DartFormat.Hex8(type);

    private void OnData(byte[] data)
    {
        var dataOffset = 0;
        var availableLength = 0;

        while (dataOffset != data.Length)
        {
            switch (_state)
            {
                case _HEADER:
                    availableLength = Math.Min(data.Length - dataOffset, _HEADER_LENGTH - _localOffset);
                    Array.Copy(data, dataOffset, _header, _localOffset, availableLength);
                    _localOffset += availableLength;

                    dataOffset += availableLength;
                    _offset = availableLength;

                    if (_localOffset != _HEADER_LENGTH)
                    {
                        break;
                    }

                    // Check glTF bytes
                    var magic = BinaryPrimitives.ReadUInt32LittleEndian(_header);
                    if (magic != _GLTF_MAGIC)
                    {
                        _context.AddIssue(GlbError.InvalidMagic, args: new object?[] { magic }, offset: 0);
                        Abort();
                        return;
                    }

                    // Check glTF version
                    var version = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(4));
                    if (version != _GLB_VERSION)
                    {
                        _context.AddIssue(GlbError.InvalidVersion, args: new object?[] { version }, offset: 4);
                        Abort();
                        return;
                    }

                    _totalLength = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(8));

                    if (_totalLength <= _offset)
                    {
                        _context.AddIssue(GlbError.LengthTooSmall, offset: 8, args: new object?[] { _totalLength });
                    }

                    _state = _CHUNK_HEADER;
                    _localOffset = 0;

                    break;

                case _CHUNK_HEADER:
                    if (_offset == _totalLength)
                    {
                        _context.AddIssue(GlbError.ExtraData, offset: _offset);
                        // Dart: _subscription.cancel(); _onDone();
                        OnDone();
                        return;
                    }

                    availableLength = Math.Min(data.Length - dataOffset, _CHUNK_HEADER_LENGTH - _localOffset);
                    Array.Copy(data, dataOffset, _header, _localOffset, availableLength);
                    _localOffset += availableLength;
                    dataOffset += availableLength;
                    _offset += availableLength;

                    if (_localOffset != _CHUNK_HEADER_LENGTH)
                    {
                        break;
                    }

                    _chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(_header);
                    _chunkType = BinaryPrimitives.ReadUInt32LittleEndian(_header.AsSpan(4));

                    if ((_chunkLength & 3) != 0)
                    {
                        _context.AddIssue(GlbError.ChunkLengthUnaligned,
                            offset: _offset - _CHUNK_HEADER_LENGTH,
                            args: new object?[] { GetChunkString(_chunkType) });
                    }

                    if (_offset + _chunkLength > _totalLength)
                    {
                        _context.AddIssue(GlbError.ChunkTooBig,
                            args: new object?[] { GetChunkString(_chunkType), _chunkLength },
                            offset: _offset - _CHUNK_HEADER_LENGTH);
                    }

                    if (_chunkNumber == 0 && _chunkType != _CHUNK_JSON)
                    {
                        _context.AddIssue(GlbError.UnexpectedFirstChunk,
                            args: new object?[] { GetChunkString(_chunkType) },
                            offset: _offset - _CHUNK_HEADER_LENGTH);
                    }

                    if (_chunkType == _CHUNK_BIN && _chunkNumber > 1 && !_hasBinChunk)
                    {
                        _context.AddIssue(GlbError.UnexpectedBinChunk,
                            args: new object?[] { GetChunkString(_chunkType) },
                            offset: _offset - _CHUNK_HEADER_LENGTH);
                    }

                    switch (_chunkType)
                    {
                        case _CHUNK_JSON:
                            // In general, chunks could have valid zero length,
                            // but not JSON chunk
                            if (_chunkLength == 0)
                            {
                                _context.AddIssue(GlbError.EmptyChunk,
                                    offset: _offset - _CHUNK_HEADER_LENGTH,
                                    args: new object?[] { GetChunkString(_chunkType) });
                            }
                            UpdateState(seen: _hasJsonChunk);
                            _hasJsonChunk = true;
                            break;
                        case _CHUNK_BIN:
                            if (_chunkLength == 0)
                            {
                                _context.AddIssue(GlbError.EmptyBinChunk,
                                    offset: _offset - _CHUNK_HEADER_LENGTH);
                            }
                            UpdateState(seen: _hasBinChunk);
                            _hasBinChunk = true;
                            break;
                        default:
                            _context.AddIssue(GlbError.UnknownChunkType,
                                args: new object?[] { GetChunkString(_chunkType) },
                                offset: _offset - _CHUNK_HEADER_LENGTH);

                            _state = _CHUNK_UNKNOWN;
                            break;
                    }

                    _chunkNumber++;
                    _localOffset = 0;
                    break;

                case _CHUNK_JSON:
                    availableLength = (int)Math.Min(data.Length - dataOffset, _chunkLength - _localOffset);

                    if (!_hasJsonReader)
                    {
                        _hasJsonReader = true;
                        _jsonStart = dataOffset;
                        _jsonLength = 0;
                    }

                    // Dart: _jsonStreamController.add(data.sublist(dataOffset, dataOffset += availableLength));
                    _jsonLength += availableLength;
                    dataOffset += availableLength;

                    _localOffset += availableLength;
                    _offset += availableLength;

                    if (_localOffset == _chunkLength)
                    {
                        _jsonStreamClosed = true;
                        _state = _CHUNK_HEADER;
                        _localOffset = 0;
                    }
                    break;

                case _CHUNK_BIN:
                    availableLength = (int)Math.Min(data.Length - dataOffset, _chunkLength - _localOffset);

                    // Dart: _binaryBuffer ??= Uint8List(_chunkLength). A truncated BIN chunk aborts the reader
                    // (the buffer is never used), so the allocation is capped to the available bytes.
                    _binaryBuffer ??= new byte[Math.Min(_chunkLength, data.Length - dataOffset)];

                    Array.Copy(data, dataOffset, _binaryBuffer, _localOffset, availableLength);
                    _localOffset += availableLength;

                    dataOffset += availableLength;
                    _offset += availableLength;

                    if (_localOffset == _chunkLength)
                    {
                        _state = _CHUNK_HEADER;
                        _localOffset = 0;
                    }
                    break;

                case _CHUNK_UNKNOWN:
                    availableLength = (int)Math.Min(data.Length - dataOffset, _chunkLength - _localOffset);

                    _localOffset += availableLength;
                    dataOffset += availableLength;
                    _offset += availableLength;

                    if (_localOffset == _chunkLength)
                    {
                        _state = _CHUNK_HEADER;
                        _localOffset = 0;
                    }
                    break;
            }
        }
    }

    private void UpdateState(bool seen)
    {
        if (seen)
        {
            _context.AddIssue(GlbError.DuplicateChunk,
                args: new object?[] { GetChunkString(_chunkType) },
                offset: _offset - _CHUNK_HEADER_LENGTH);
            _state = _CHUNK_UNKNOWN;
        }
        else
        {
            _state = _chunkType;
        }
    }

    private void OnDone()
    {
        switch (_state)
        {
            case _HEADER:
                _context.AddIssue(GlbError.UnexpectedEndOfHeader, offset: _offset);
                Abort();
                break;

            case _CHUNK_HEADER:
                if (_localOffset != 0)
                {
                    _context.AddIssue(GlbError.UnexpectedEndOfChunkHeader, offset: _offset);
                    Abort();
                }
                else
                {
                    if (_totalLength != _offset)
                    {
                        _context.AddIssue(GlbError.LengthMismatch,
                            args: new object?[] { _totalLength, _offset }, offset: _offset);
                    }

                    if (_hasJsonReader)
                    {
                        // Dart: _jsonReaderResult.then((result) => complete(GltfReaderResult(mimeType, result?.gltf, _binaryBuffer)))
                        CompleteWithJsonResult();
                        if (_jsonProcessed)
                        {
                            _result = new GltfReaderResult(MimeType, _jsonGltf, _binaryBuffer);
                        }
                    }
                    else
                    {
                        _completed = true;
                        _result = new GltfReaderResult(MimeType, null, _binaryBuffer);
                    }
                }
                break;

            default:
                if (_chunkLength > 0)
                {
                    _context.AddIssue(GlbError.UnexpectedEndOfChunkData, offset: _offset);
                }
                Abort();
                break;
        }
    }
}
