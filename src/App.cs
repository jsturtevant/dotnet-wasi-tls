using System;
using System.IO;
using System.Text;
using System.Threading;
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
                using var passthrough = new PassthroughSTream(tcpStream);
                using var sslStream = new SslStream(passthrough);
                await sslStream.AuthenticateAsClientAsync(host);
                await sslStream.WriteAsync(
                    Encoding.UTF8.GetBytes(
                        $"GET / HTTP/1.1\r\nhost: {addressString}\r\nconnection: close\r\n\r\n"
                    )
                );
                var response = new MemoryStream();
                await sslStream.CopyToAsync(response);
                //Console.WriteLine(Encoding.UTF8.GetString(response.GetBuffer()));
                Console.WriteLine("done");
                return;
            }
        }
        throw new Exception($"unable to parse \"{addressString}\" as <host>:<port> pair");
    }

    public class PassthroughSTream : Stream
    {
        private Stream stream;

        public PassthroughSTream(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => this.stream.CanRead;

        public override bool CanSeek => this.stream.CanSeek;

        public override bool CanWrite => this.stream.CanWrite;

        public override long Length => this.stream.Length;

        public override long Position { get => this.stream.Position; set => this.stream.Position = value; }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(
           byte[] bytes,
           int offset,
           int length,
           CancellationToken cancellationToken
           )
        {
            Console.WriteLine("PAAAAAAAAAAAAAAAAAASSSSSSSSSSSSSSSSS");
            return await this.stream.ReadAsync(bytes, offset, length, cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
        {
            Console.WriteLine("PAAAAAAAAAAAAAAAAAASSSSSSSSSSSSSSSSS");
            return await this.stream.ReadAsync(buffer, cancellationToken);
        }

        public override async Task WriteAsync(
           byte[] bytes,
           int offset,
           int length,
           CancellationToken cancellationToken
       )
        {
            Console.WriteLine("PAAAAAAAAAAAAAAAAAASSSSSSSSSSSSSSSSS");
            await this.stream.WriteAsync(bytes, offset, length, cancellationToken);
        }
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            Console.WriteLine("PAAAAAAAAAAAAAAAAAASSSSSSSSSSSSSSSSS");
            return this.stream.WriteAsync(buffer, cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Console.WriteLine("COPY TO");
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
}
