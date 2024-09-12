using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

public class App
{
    public static int Main(string[] args)
    {
        var task = MainAsync(args[0]);
        while (!task.IsCompleted)
        {
            WasiEventLoop.Dispatch();
        }
        var exception = task.Exception;
        if (exception is not null)
        {
            throw exception;
        }
        return 0;
    }

    private static async Task<List<INetwork.IpSocketAddress>> Resolve(
        INetwork.Network network,
        string addressString
    )
    {
        if (IPEndPoint.TryParse(addressString, out IPEndPoint? endpoint))
        {
            switch (endpoint.Address.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                {
                    var ip = endpoint.Address.GetAddressBytes();
                    return new List<INetwork.IpSocketAddress>
                    {
                        INetwork.IpSocketAddress.ipv4(
                            new INetwork.Ipv4SocketAddress(
                                (ushort)endpoint.Port,
                                (ip[0], ip[1], ip[2], ip[3])
                            )
                        )
                    };
                }
                case AddressFamily.InterNetworkV6:
                {
                    var ip = endpoint.Address.GetAddressBytes();
                    return new List<INetwork.IpSocketAddress>
                    {
                        INetwork.IpSocketAddress.ipv6(
                            new INetwork.Ipv6SocketAddress(
                                (ushort)endpoint.Port,
                                0,
                                (
                                    (ushort)((((ushort)ip[0]) << 8) | ip[1]),
                                    (ushort)((((ushort)ip[2]) << 8) | ip[3]),
                                    (ushort)((((ushort)ip[4]) << 8) | ip[5]),
                                    (ushort)((((ushort)ip[6]) << 8) | ip[7]),
                                    (ushort)((((ushort)ip[8]) << 8) | ip[9]),
                                    (ushort)((((ushort)ip[10]) << 8) | ip[11]),
                                    (ushort)((((ushort)ip[12]) << 8) | ip[13]),
                                    (ushort)((((ushort)ip[14]) << 8) | ip[15])
                                ),
                                0
                            )
                        )
                    };
                }
                default:
                    throw new Exception(
                        $"unexpected address family: {endpoint.Address.AddressFamily}"
                    );
            }
        }
        else
        {
            var tokens = addressString.Split(':');
            if (tokens.Length == 2)
            {
                if (ushort.TryParse(tokens[1], out ushort port))
                {
                    var stream = IpNameLookupInterop.ResolveAddresses(network, tokens[0]);
                    var list = new List<INetwork.IpSocketAddress>();
                    while (true)
                    {
                        try
                        {
                            var address = stream.ResolveNextAddress();
                            if (address is not null)
                            {
                                switch (address.Tag)
                                {
                                    case INetwork.IpAddress.IPV4:
                                    {
                                        list.Add(
                                            INetwork.IpSocketAddress.ipv4(
                                                new INetwork.Ipv4SocketAddress(port, address.AsIpv4)
                                            )
                                        );
                                        break;
                                    }
                                    case INetwork.IpAddress.IPV6:
                                    {
                                        list.Add(
                                            INetwork.IpSocketAddress.ipv6(
                                                new INetwork.Ipv6SocketAddress(
                                                    port,
                                                    0,
                                                    address.AsIpv6,
                                                    0
                                                )
                                            )
                                        );
                                        break;
                                    }
                                    default:
                                        throw new Exception(
                                            $"unexpected IpAddress tag: {address.Tag}"
                                        );
                                }
                            }
                            else
                            {
                                return list;
                            }
                        }
                        catch (WitException e)
                        {
                            switch ((INetwork.ErrorCode)e.Value)
                            {
                                case INetwork.ErrorCode.WOULD_BLOCK:
                                {
                                    await WasiEventLoop.Register(
                                        stream.Subscribe(),
                                        CancellationToken.None
                                    );
                                    break;
                                }
                                default:
                                    throw;
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception($"unable to parse \"{tokens[1]}\" as ushort");
                }
            }
            else
            {
                throw new Exception(
                    $"unable to parse \"{addressString}\" as a <hostname>:<port> tuple"
                );
            }
        }
    }

    private static async Task<Stream> Connect(
        INetwork.Network network,
        INetwork.IpSocketAddress address
    )
    {
        INetwork.IpAddressFamily family;
        switch (address.Tag)
        {
            case INetwork.IpSocketAddress.IPV4:
            {
                family = INetwork.IpAddressFamily.IPV4;
                break;
            }
            case INetwork.IpSocketAddress.IPV6:
            {
                family = INetwork.IpAddressFamily.IPV6;
                break;
            }
            default:
                throw new Exception($"unexpected IpSocketAddress tag: {address.Tag}");
        }

        var client = TcpCreateSocketInterop.CreateTcpSocket(family);
        try
        {
            client.StartConnect(network, address);
            while (true)
            {
                try
                {
                    var (rx, tx) = client.FinishConnect();
                    return new TcpStream(client, rx, tx);
                }
                catch (WitException e)
                {
                    switch ((INetwork.ErrorCode)e.Value)
                    {
                        case INetwork.ErrorCode.WOULD_BLOCK:
                        {
                            await WasiEventLoop.Register(
                                client.Subscribe(),
                                CancellationToken.None
                            );
                            break;
                        }
                        default:
                            throw;
                    }
                }
            }
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task MainAsync(string addressString)
    {
        var colonIndex = addressString.LastIndexOf(':');
        string hostName = addressString.Substring(
            0,
            colonIndex > 0 ? colonIndex : addressString.Length
        );
        var network = InstanceNetworkInterop.InstanceNetwork();
        var addresses = await Resolve(network, addressString);
        foreach (var address in addresses)
        {
            Stream tcpStream;
            try
            {
                tcpStream = await Connect(network, address);
            }
            catch
            {
                // try the next one
                continue;
            }
            await using var stream = new SslStream(tcpStream);
            stream.AuthenticateAsClientAsync(hostName);
            await stream.WriteAsync(
                Encoding.UTF8.GetBytes(
                    $"GET / HTTP/1.1\r\nhost: {addressString}\r\nconnection: close\r\n\r\n"
                )
            );
            var response = new MemoryStream();
            await stream.CopyToAsync(response);
            Console.WriteLine(Encoding.UTF8.GetString(response.GetBuffer()));
        }
    }

    internal static class WasiEventLoop
    {
        internal static void Dispatch()
        {
            CallDispatchWasiEventLoop((Thread)null!);

            [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "DispatchWasiEventLoop")]
            static extern void CallDispatchWasiEventLoop(Thread t);
        }

        internal static Task Register(IPoll.Pollable pollable, CancellationToken cancellationToken)
        {
            var handle = pollable.Handle;
            pollable.Handle = 0;
            return CallRegister((Thread)null!, handle, cancellationToken);

            [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "RegisterWasiPollableHandle")]
            static extern Task CallRegister(
                Thread t,
                int handle,
                CancellationToken cancellationToken
            );
        }
    }

    internal class TcpStream : Stream
    {
        private ITcp.TcpSocket client;
        private IStreams.InputStream input;
        private IStreams.OutputStream output;
        private int offset;
        private byte[]? buffer;
        private bool closed;

        public TcpStream(
            ITcp.TcpSocket client,
            IStreams.InputStream input,
            IStreams.OutputStream output
        )
        {
            this.client = client;
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
            client.Dispose();
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
}
