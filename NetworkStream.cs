using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;

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
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int length)
    {
        throw new NotImplementedException();
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
                    if (((IStreams.StreamError)e.Value).Tag == IStreams.StreamError.CLOSED)
                    {
                        closed = true;
                        return 0;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            else
            {
                var min = Math.Min(this.buffer.Length - this.offset, length);
                Array.Copy(this.buffer, this.offset, bytes, offset, min);
                if (min < buffer.Length - this.offset)
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
            var count = (int)output.CheckWrite();
            if (count == 0)
            {
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
                    output.Write(bytes);
                }
                else
                {
                    // TODO: is there a more efficient option than copying here?
                    // Do we need to change the binding generator to accept
                    // e.g. `Span`s?
                    var copy = new byte[min];
                    Array.Copy(bytes, offset, copy, 0, min);
                    output.Write(copy);
                }
                offset += min;
            }
        }
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: avoid copy when possible and use ArrayPool when not
        var copy = new byte[buffer.Length];
        buffer.Span.CopyTo(copy);
        return new ValueTask(WriteAsync(copy, 0, buffer.Length, cancellationToken));
    }
}
