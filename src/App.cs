using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wasi.Tls;
using System.Runtime.CompilerServices;

public class App
{
     public static int Main(string[] args)
    {
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args[0]));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }



    private static async Task<int> MainAsync(string addressString)
    {
        var colonIndex = addressString.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var host = addressString.Substring(0, colonIndex);
            if (ushort.TryParse(addressString.Substring(colonIndex + 1), out ushort port))
            {
                using var client = new Wasi.Tls.TcpClient();
                await client.ConnectAsync(host, port);
                using var tcpStream = client.GetStream();
                using var sslStream = new SslStream(tcpStream);
                await sslStream.AuthenticateAsClientAsync(host);
                await sslStream.WriteAsync(
                    Encoding.UTF8.GetBytes(
                        $"GET / HTTP/1.1\r\nhost: {addressString}\r\nconnection: close\r\n\r\n"
                    )
                );
                var response = new MemoryStream();
                await sslStream.CopyToAsync(response);
                Console.WriteLine(Encoding.UTF8.GetString(response.GetBuffer()));
                return 0;
            }
        }
        throw new Exception($"unable to parse \"{addressString}\" as <host>:<port> pair");
    }
}
