using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

namespace Wasi.Tls {
public class TcpClient : IDisposable
{
    private NetworkStream? stream;

    public void Dispose()
    {
        Dispose(true);
    }

    protected void Dispose(bool disposing)
    {
        stream?.Dispose();
    }

    public void Connect(IPAddress[] addresses, int port)
    {
        WasiEventLoop.RunAsync(() => ConnectAsync(addresses, port));
    }

    public async Task ConnectAsync(IPAddress[] addresses, int port)
    {
        using var network = InstanceNetworkInterop.InstanceNetwork();
        Exception? exception = null;
        foreach (var address in addresses)
        {
            try
            {
                stream = await Connect(network, IntoIpSocketAddress(new IPEndPoint(address, port)));
                return;
            }
            catch (Exception e)
            {
                exception = e;
                // try the next one
                continue;
            }
        }
        throw exception ?? new Exception("no addresses provided");
    }

    public async Task ConnectAsync(string host, int port)
    {
        await ConnectAsync(await Dns.GetHostAddressesAsync(host), port);
    }

    public NetworkStream GetStream()
    {
        if (stream is not null)
        {
            return stream;
        }
        else
        {
            throw new InvalidOperationException("TcpClient is not yet connected.");
        }
    }

    private static INetwork.IpSocketAddress IntoIpSocketAddress(IPEndPoint endpoint)
    {
        switch (endpoint.Address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
            {
                var ip = endpoint.Address.GetAddressBytes();
                return INetwork.IpSocketAddress.ipv4(
                    new INetwork.Ipv4SocketAddress(
                        (ushort)endpoint.Port,
                        (ip[0], ip[1], ip[2], ip[3])
                    )
                );
            }
            case AddressFamily.InterNetworkV6:
            {
                var ip = endpoint.Address.GetAddressBytes();
                return INetwork.IpSocketAddress.ipv6(
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
                );
            }
            default:
                throw new Exception($"unexpected address family: {endpoint.Address.AddressFamily}");
        }
    }

    private static async Task<NetworkStream> Connect(
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
                    var value = (INetwork.ErrorCode)e.Value;
                    switch (value)
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
                            throw new Exception($"error when connecting to {address}: {value}");
                    }
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
            client.Dispose();
            throw;
        }
    }

    internal class TcpStream : NetworkStream
    {
        private ITcp.TcpSocket client;

        internal TcpStream(
            ITcp.TcpSocket client,
            IStreams.InputStream input,
            IStreams.OutputStream output
        )
            : base(input, output)
        {
            this.client = client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            client.Dispose();
        }
    }
}
}