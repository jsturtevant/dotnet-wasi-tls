// Generated by `wit-bindgen` 0.39.0. DO NOT EDIT!
// <auto-generated />
#nullable enable

using System;
using System.Runtime.InteropServices;

namespace ImportsWorld.wit.imports.wasi.sockets.v0_2_1;

public interface INetwork {

    /**
    * Error codes.
    *
    * In theory, every API can return any error code.
    * In practice, API's typically only return the errors documented per API
    * combined with a couple of errors that are always possible:
    * - `unknown`
    * - `access-denied`
    * - `not-supported`
    * - `out-of-memory`
    * - `concurrency-conflict`
    *
    * See each individual API for what the POSIX equivalents are. They sometimes differ per API.
    */

    public enum ErrorCode {
        UNKNOWN, ACCESS_DENIED, NOT_SUPPORTED, INVALID_ARGUMENT, OUT_OF_MEMORY, TIMEOUT, CONCURRENCY_CONFLICT, NOT_IN_PROGRESS, WOULD_BLOCK, INVALID_STATE, NEW_SOCKET_LIMIT, ADDRESS_NOT_BINDABLE, ADDRESS_IN_USE, REMOTE_UNREACHABLE, CONNECTION_REFUSED, CONNECTION_RESET, CONNECTION_ABORTED, DATAGRAM_TOO_LARGE, NAME_UNRESOLVABLE, TEMPORARY_RESOLVER_FAILURE, PERMANENT_RESOLVER_FAILURE
    }

    public enum IpAddressFamily {
        IPV4, IPV6
    }

    public class IpAddress {
        public readonly byte Tag;
        private readonly object? value;

        private IpAddress(byte tag, object? value) {
            this.Tag = tag;
            this.value = value;
        }

        public static IpAddress Ipv4((byte, byte, byte, byte) ipv4) {
            return new IpAddress(Tags.Ipv4, ipv4);
        }

        public static IpAddress Ipv6((ushort, ushort, ushort, ushort, ushort, ushort, ushort, ushort) ipv6) {
            return new IpAddress(Tags.Ipv6, ipv6);
        }

        public (byte, byte, byte, byte) AsIpv4
        {
            get
            {
                if (Tag == Tags.Ipv4)
                return ((byte, byte, byte, byte))value!;
                else
                throw new ArgumentException("expected Ipv4, got " + Tag);
            }
        }

        public (ushort, ushort, ushort, ushort, ushort, ushort, ushort, ushort) AsIpv6
        {
            get
            {
                if (Tag == Tags.Ipv6)
                return ((ushort, ushort, ushort, ushort, ushort, ushort, ushort, ushort))value!;
                else
                throw new ArgumentException("expected Ipv6, got " + Tag);
            }
        }

        public class Tags {
            public const byte Ipv4 = 0;
            public const byte Ipv6 = 1;
        }
    }

    public class Ipv4SocketAddress {
        public readonly ushort port;
        public readonly (byte, byte, byte, byte) address;

        public Ipv4SocketAddress(ushort port, (byte, byte, byte, byte) address) {
            this.port = port;
            this.address = address;
        }
    }

    public class Ipv6SocketAddress {
        public readonly ushort port;
        public readonly uint flowInfo;
        public readonly (ushort, ushort, ushort, ushort, ushort, ushort, ushort, ushort) address;
        public readonly uint scopeId;

        public Ipv6SocketAddress(ushort port, uint flowInfo, (ushort, ushort, ushort, ushort, ushort, ushort, ushort, ushort) address, uint scopeId) {
            this.port = port;
            this.flowInfo = flowInfo;
            this.address = address;
            this.scopeId = scopeId;
        }
    }

    public class IpSocketAddress {
        public readonly byte Tag;
        private readonly object? value;

        private IpSocketAddress(byte tag, object? value) {
            this.Tag = tag;
            this.value = value;
        }

        public static IpSocketAddress Ipv4(Ipv4SocketAddress ipv4) {
            return new IpSocketAddress(Tags.Ipv4, ipv4);
        }

        public static IpSocketAddress Ipv6(Ipv6SocketAddress ipv6) {
            return new IpSocketAddress(Tags.Ipv6, ipv6);
        }

        public Ipv4SocketAddress AsIpv4
        {
            get
            {
                if (Tag == Tags.Ipv4)
                return (Ipv4SocketAddress)value!;
                else
                throw new ArgumentException("expected Ipv4, got " + Tag);
            }
        }

        public Ipv6SocketAddress AsIpv6
        {
            get
            {
                if (Tag == Tags.Ipv6)
                return (Ipv6SocketAddress)value!;
                else
                throw new ArgumentException("expected Ipv6, got " + Tag);
            }
        }

        public class Tags {
            public const byte Ipv4 = 0;
            public const byte Ipv6 = 1;
        }
    }

    /**
    * An opaque resource that represents access to (a subset of) the network.
    * This enables context-based security for networking.
    * There is no need for this to map 1:1 to a physical network interface.
    */

    public class Network: IDisposable {
        internal int Handle { get; set; }

        public readonly record struct THandle(int Handle);

        public Network(THandle handle) {
            Handle = handle.Handle;
        }

        public void Dispose() {
            Dispose(true);
        }

        [DllImport("wasi:sockets/network@0.2.1", EntryPoint = "[resource-drop]network"), WasmImportLinkage]
        private static extern void wasmImportResourceDrop(int p0);

        protected virtual void Dispose(bool disposing) {
            if (disposing && Handle != 0) {
                wasmImportResourceDrop(Handle);
                Handle = 0;
            }
        }

    }

}
