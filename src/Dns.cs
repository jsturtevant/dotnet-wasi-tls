using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

namespace Wasi.Tls
{
    public class Dns
    {

        public static IPAddress[] GetHostAddresses(string hostNameOrAddress)
        {
            return GetHostAddressesAsync(hostNameOrAddress).Result;
        }
        
        public static async Task<IPAddress[]> GetHostAddressesAsync(string name)
        {
            if (IPAddress.TryParse(name, out IPAddress? parsed))
            {
                return new IPAddress[] { parsed };
            }
            else
            {
                using var network = InstanceNetworkInterop.InstanceNetwork();
                using var stream = IpNameLookupInterop.ResolveAddresses(network, name);
                var list = new List<IPAddress>();
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
                                        var (ip0, ip1, ip2, ip3) = address.AsIpv4;
                                        list.Add(new IPAddress(new byte[] { ip0, ip1, ip2, ip3 }));
                                        break;
                                    }
                                case INetwork.IpAddress.IPV6:
                                    {
                                        var (ip0, ip1, ip2, ip3, ip4, ip5, ip6, ip7) = address.AsIpv6;
                                        list.Add(
                                            new IPAddress(
                                                new byte[]
                                                {
                                            (byte)(ip0 >> 8),
                                            (byte)(ip0 & 0xFF),
                                            (byte)(ip1 >> 8),
                                            (byte)(ip1 & 0xFF),
                                            (byte)(ip2 >> 8),
                                            (byte)(ip2 & 0xFF),
                                            (byte)(ip3 >> 8),
                                            (byte)(ip3 & 0xFF),
                                            (byte)(ip4 >> 8),
                                            (byte)(ip4 & 0xFF),
                                            (byte)(ip5 >> 8),
                                            (byte)(ip5 & 0xFF),
                                            (byte)(ip6 >> 8),
                                            (byte)(ip6 & 0xFF),
                                            (byte)(ip7 >> 8),
                                            (byte)(ip7 & 0xFF),
                                                }
                                            )
                                        );
                                        break;
                                    }
                                default:
                                    throw new Exception($"unexpected IpAddress tag: {address.Tag}");
                            }
                        }
                        else
                        {
                            return list.ToArray();
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
        }
    }
}