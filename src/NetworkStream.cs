using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;

namespace Wasi.Tls {


public class NetworkStream : Stream
{
    internal IStreams.InputStream input;
    internal IStreams.OutputStream output;
    private int offset;
    private byte[]? buffer;
    private bool closed;

    internal NetworkStream(IStreams.InputStream input, IStreams.OutputStream output)
    {
        this.input = input;
        this.output = output;
    }
    public bool Connected  => this.closed;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotImplementedException();
    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public new void Dispose()
    {
        Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        input.Dispose();
        output.Dispose();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        // ignore
    }

    public override void SetLength(long length)
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int length)
    {
        return WasiEventLoop.RunAsync(() => ReadAsync(buffer, offset, length, CancellationToken.None));
    }

    public override void Write(byte[] buffer, int offset, int length)
    {
        WasiEventLoop.RunAsync(() => WriteAsync(buffer, offset, length, CancellationToken.None));
    }

    public override async Task<int> ReadAsync(
        byte[] bytes,
        int offset,
        int length,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            if (closed)
            {
                return 0;
            }
            else if (this.buffer == null)
            {
                try
                {
                    // TODO: should we add a special case to the bindings generator
                    // to allow passing a buffer to IStreams.InputStream.Read and
                    // avoid the extra copy?
                    var result = input.Read(16 * 1024);
                    var buffer = result;
                    if (buffer.Length == 0)
                    {
                        Console.WriteLine("networkstream aysnc Registering for read");
                        await WasiEventLoop
                            .Register(input.Subscribe(), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        this.buffer = buffer;
                        this.offset = 0;
                    }
                }
                catch (WitException e)
                {
                    var value = (IStreams.StreamError)e.Value;
                    if (value.Tag == IStreams.StreamError.CLOSED)
                    {
                        closed = true;
                        return 0;
                    }
                    else
                    {
                        throw new Exception(
                            $"read error: {value.AsLastOperationFailed.ToDebugString()}"
                        );
                    }
                }
            }
            else
            {
                var min = Math.Min(this.buffer.Length - this.offset, length);
                Console.WriteLine("networkstream read async {0} bytes", min);
                Array.Copy(this.buffer, this.offset, bytes, offset, min);
                if (min < this.buffer.Length - this.offset)
                {
                    this.offset += min;
                }
                else
                {
                    this.buffer = null;
                }
                return min;
            }
        }
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: avoid copy when possible and use ArrayPool when not
        var dst = new byte[buffer.Length];
        Console.WriteLine("networkstream reading data async");
        Console.WriteLine("networkstream read {0} bytes value", buffer.Length);
        var result = await ReadAsync(dst, 0, buffer.Length, cancellationToken);
        new ReadOnlySpan<byte>(dst, 0, result).CopyTo(buffer.Span);
        return result;
    }

    public override async Task WriteAsync(
        byte[] bytes,
        int offset,
        int length,
        CancellationToken cancellationToken
    )
    {
        var limit = offset + length;
        var flushing = false;
        while (true)
        {
            int count;
            try
            {
                count = (int)output.CheckWrite();
            }
            catch (WitException e)
            {
                throw ConvertException(e);
            }
            if (count == 0)
            {
                Console.WriteLine("networkstream Registering for output");
                await WasiEventLoop.Register(output.Subscribe(), cancellationToken);
            }
            else if (offset == limit)
            {
                if (flushing)
                {
                    return;
                }
                else
                {
                    output.Flush();
                    flushing = true;
                }
            }
            else
            {
                var min = Math.Min(count, limit - offset);
                if (offset == 0 && min == bytes.Length)
                {
                    try
                    {
                        output.Write(bytes);
                    }
                    catch (WitException e)
                    {
                        throw ConvertException(e);
                    }
                }
                else
                {
                    // TODO: is there a more efficient option than copying here?
                    // Do we need to change the binding generator to accept
                    // e.g. `Span`s?
                    var copy = new byte[min];
                    Array.Copy(bytes, offset, copy, 0, min);
                    Console.WriteLine("networkstream Writing data of length {0}", min);
                    Console.WriteLine("networkstream Writing data {0}", copy);
                    output.Write(copy);
                }
                offset += min;
            }
        }
    }

    private static Exception ConvertException(WitException e)
    {
        var value = (IStreams.StreamError)e.Value;
        if (value.Tag == IStreams.StreamError.CLOSED)
        {
            return new Exception("write error: stream closed unexpectedly");
        }
        else
        {
            return new Exception($"write error: {value.AsLastOperationFailed.ToDebugString()}");
        }
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        Console.WriteLine("networkstream Write async");
        // TODO: avoid copy when possible and use ArrayPool when not
        var copy = new byte[buffer.Length];
        buffer.Span.CopyTo(copy);
        return new ValueTask(WriteAsync(copy, 0, buffer.Length, cancellationToken));
    }
}
}