using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Wasi.Tls;

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

    private static async Task MainAsync(string addressString)
    {
        var colonIndex = addressString.LastIndexOf(':');
        if (colonIndex > 0)
        {
            var host = addressString.Substring(0, colonIndex);
            if (ushort.TryParse(addressString.Substring(colonIndex + 1), out ushort port))
            {
                using var client = new TcpClient();
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
                return;
            }
        }
        throw new Exception($"unable to parse \"{addressString}\" as <host>:<port> pair");
    }
}
