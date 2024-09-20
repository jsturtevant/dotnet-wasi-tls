using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImportsWorld;
using ImportsWorld.wit.imports.wasi.io.v0_2_1;
using ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

namespace Wasi.Tls
{
    public class SslStream : Stream
    {
        private static string authErrorMessage =
            "This operation is only allowed using a successfully authenticated context.";

        private Stream cipherStream;
        private Stream? plainStream;

        public SslStream(Stream cipherStream)
        {
            this.cipherStream = cipherStream;
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
            cipherStream.Dispose();
            plainStream?.Dispose();
        }

        public void AuthenticateAsClient(string host){
            WasiEventLoop.RunAsync(() => AuthenticateAsClientAsync(host));
        }

        public async Task AuthenticateAsClientAsync(string host)
        {
            IStreams.InputStream cipherInput;
            IStreams.OutputStream cipherOutput;
            if (cipherStream is NetworkStream networkStream)
            {
                cipherInput = networkStream.input;
                cipherOutput = networkStream.output;
            }
            else
            {
                var (inputA, outputA) = TlsInterop.MakePipe();
                var (inputB, outputB) = TlsInterop.MakePipe();
                cipherInput = inputA;
                cipherOutput = outputB;
                var proxy = new NetworkStream(inputB, outputA);
                _ = proxy.CopyToAsync(cipherStream);
                _ = cipherStream.CopyToAsync(proxy);
            }

            using var future = ITls.ClientHandshake.Finish(
                new ITls.ClientConnection(cipherInput, cipherOutput).Connect(host)
            );

            while (true)
            {
                var result = future.Get();
                if (result is not null)
                {
                    var inner = (
                        (Result<Result<(IStreams.InputStream, IStreams.OutputStream), None>, None>)
                            result!
                    ).AsOk;
                    if (inner.IsOk)
                    {
                        var (input, output) = inner.AsOk;
                        plainStream = new NetworkStream(input, output);
                        break;
                    }
                    else
                    {
                        throw new Exception("TLS handshake failed");
                    }
                }
                else
                {
                    await WasiEventLoop.Register(future.Subscribe(), CancellationToken.None);
                }
            }
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
            if (plainStream is not null)
            {
                return plainStream.Read(buffer, offset, length);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }

        public override void Write(byte[] buffer, int offset, int length)
        {
            if (plainStream is not null)
            {
                plainStream.Write(buffer, offset, length);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }

        public override Task<int> ReadAsync(
            byte[] bytes,
            int offset,
            int length,
            CancellationToken cancellationToken
        )
        {
            if (plainStream is not null)
            {
                return plainStream.ReadAsync(bytes, offset, length, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (plainStream is not null)
            {
                return plainStream.ReadAsync(buffer, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }

        public override Task WriteAsync(
            byte[] bytes,
            int offset,
            int length,
            CancellationToken cancellationToken
        )
        {
            if (plainStream is not null)
            {
                return plainStream.WriteAsync(bytes, offset, length, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (plainStream is not null)
            {
                return plainStream.WriteAsync(buffer, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(authErrorMessage);
            }
        }
    }
}
