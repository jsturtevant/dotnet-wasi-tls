// Generated by `wit-bindgen` 0.39.0. DO NOT EDIT!
// <auto-generated />
#nullable enable

using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;

namespace ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

public interface ITls {

    public class ClientConnection: IDisposable {
        internal int Handle { get; set; }

        public readonly record struct THandle(int Handle);

        public ClientConnection(THandle handle) {
            Handle = handle.Handle;
        }

        public void Dispose() {
            Dispose(true);
        }

        [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[resource-drop]client-connection"), WasmImportLinkage]
        private static extern void wasmImportResourceDrop(int p0);

        protected virtual void Dispose(bool disposing) {
            if (disposing && Handle != 0) {
                wasmImportResourceDrop(Handle);
                Handle = 0;
            }
        }

        internal static class ConstructorWasmInterop
        {
            [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[constructor]client-connection"), WasmImportLinkage]
            internal static extern int wasmImportConstructor(int p0, int p1);
        }

        public   unsafe  ClientConnection(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream input, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream output)
        {
            var handle = input.Handle;
            input.Handle = 0;
            var handle0 = output.Handle;
            output.Handle = 0;
            var result =  ConstructorWasmInterop.wasmImportConstructor(handle, handle0);
            this.Handle = result;

        }

        internal static class ConnectWasmInterop
        {
            [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[method]client-connection.connect"), WasmImportLinkage]
            internal static extern void wasmImportConnect(int p0, nint p1, int p2, nint p3);
        }

        public   unsafe global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake Connect(string serverName)
        {
            var cleanups = new List<Action>();
            var handle = this.Handle;

            var utf8Bytes = Encoding.UTF8.GetBytes(serverName);
            var length = utf8Bytes.Length;
            var gcHandle = GCHandle.Alloc(utf8Bytes, GCHandleType.Pinned);
            var interopString = gcHandle.AddrOfPinnedObject();

            cleanups.Add(()=> gcHandle.Free());

            var retArea = stackalloc uint[2+1];
            var ptr = ((int)retArea) + (4 - 1) & -4;
            ConnectWasmInterop.wasmImportConnect(handle, interopString.ToInt32(), length, ptr);

            Result<global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake, None> lifted;

            switch (new Span<byte>((void*)(ptr + 0), 1)[0]) {
                case 0: {
                    var resource = new global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake(new global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake.THandle(BitConverter.ToInt32(new Span<byte>((void*)(ptr + 4), 4))));

                    lifted = Result<global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake, None>.Ok(resource);
                    break;
                }
                case 1: {

                    lifted = Result<global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake, None>.Err(new global::ImportsWorld.None());
                    break;
                }

                default: throw new ArgumentException($"invalid discriminant: {new Span<byte>((void*)(ptr + 0), 1)[0]}");
            }

            foreach (var cleanup in cleanups)
            {
                cleanup();
            }
            if (lifted.IsOk)
            {
                var tmp = lifted.AsOk;
                return tmp;
            }
            else
            {
                throw new WitException(lifted.AsErr!, 0);
            }

        }

    }

    public class ClientHandshake: IDisposable {
        internal int Handle { get; set; }

        public readonly record struct THandle(int Handle);

        public ClientHandshake(THandle handle) {
            Handle = handle.Handle;
        }

        public void Dispose() {
            Dispose(true);
        }

        [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[resource-drop]client-handshake"), WasmImportLinkage]
        private static extern void wasmImportResourceDrop(int p0);

        protected virtual void Dispose(bool disposing) {
            if (disposing && Handle != 0) {
                wasmImportResourceDrop(Handle);
                Handle = 0;
            }
        }

        internal static class FinishWasmInterop
        {
            [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[static]client-handshake.finish"), WasmImportLinkage]
            internal static extern int wasmImportFinish(int p0);
        }

        public  static unsafe global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.FutureStreams Finish(global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.ClientHandshake @this)
        {
            var handle = @this.Handle;
            @this.Handle = 0;
            var result =  FinishWasmInterop.wasmImportFinish(handle);
            var resource = new global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.FutureStreams(new global::ImportsWorld.wit.imports.wasi.sockets.v0_2_1.ITls.FutureStreams.THandle(result));
            return resource;

        }

    }

    public class FutureStreams: IDisposable {
        internal int Handle { get; set; }

        public readonly record struct THandle(int Handle);

        public FutureStreams(THandle handle) {
            Handle = handle.Handle;
        }

        public void Dispose() {
            Dispose(true);
        }

        [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[resource-drop]future-streams"), WasmImportLinkage]
        private static extern void wasmImportResourceDrop(int p0);

        protected virtual void Dispose(bool disposing) {
            if (disposing && Handle != 0) {
                wasmImportResourceDrop(Handle);
                Handle = 0;
            }
        }

        internal static class SubscribeWasmInterop
        {
            [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[method]future-streams.subscribe"), WasmImportLinkage]
            internal static extern int wasmImportSubscribe(int p0);
        }

        public   unsafe global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IPoll.Pollable Subscribe()
        {
            var handle = this.Handle;
            var result =  SubscribeWasmInterop.wasmImportSubscribe(handle);
            var resource = new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IPoll.Pollable(new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IPoll.Pollable.THandle(result));
            return resource;

        }

        internal static class GetWasmInterop
        {
            [DllImport("wasi:sockets/tls@0.2.1", EntryPoint = "[method]future-streams.get"), WasmImportLinkage]
            internal static extern void wasmImportGet(int p0, nint p1);
        }

        public   unsafe Result<Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>, None>? Get()
        {
            var handle = this.Handle;

            var retArea = stackalloc uint[5+1];
            var ptr = ((int)retArea) + (4 - 1) & -4;
            GetWasmInterop.wasmImportGet(handle, ptr);

            Result<Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>, None>? lifted12;

            switch (new Span<byte>((void*)(ptr + 0), 1)[0]) {
                case 0: {
                    lifted12 = null;
                    break;
                }

                case 1: {

                    Result<Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>, None> lifted11;

                    switch (new Span<byte>((void*)(ptr + 4), 1)[0]) {
                        case 0: {

                            Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None> lifted;

                            switch (new Span<byte>((void*)(ptr + 8), 1)[0]) {
                                case 0: {
                                    var resource = new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream(new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream.THandle(BitConverter.ToInt32(new Span<byte>((void*)(ptr + 12), 4))));
                                    var resource6 = new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream(new global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream.THandle(BitConverter.ToInt32(new Span<byte>((void*)(ptr + 16), 4))));

                                    lifted = Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>.Ok((resource, resource6));
                                    break;
                                }
                                case 1: {

                                    lifted = Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>.Err(new global::ImportsWorld.None());
                                    break;
                                }

                                default: throw new ArgumentException($"invalid discriminant: {new Span<byte>((void*)(ptr + 8), 1)[0]}");
                            }

                            lifted11 = Result<Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>, None>.Ok(lifted);
                            break;
                        }
                        case 1: {

                            lifted11 = Result<Result<(global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.InputStream, global::ImportsWorld.wit.imports.wasi.io.v0_2_1.IStreams.OutputStream), None>, None>.Err(new global::ImportsWorld.None());
                            break;
                        }

                        default: throw new ArgumentException($"invalid discriminant: {new Span<byte>((void*)(ptr + 4), 1)[0]}");
                    }

                    lifted12 = lifted11;
                    break;
                }

                default: throw new ArgumentException("invalid discriminant: " + (new Span<byte>((void*)(ptr + 0), 1)[0]));
            }
            return lifted12;

        }

    }

}
