# .NET `SslStream` on WASI experiment

This demonstrates the feasibility of implementing a subset of .NET's
`System.Net.Security.SslStream` based on [a proposed `wasi:sockets/tls`
interface](https://github.com/WebAssembly/wasi-sockets/pull/104)

## Prequisites

- Rust
- .NET 9 Preview 7 or later

Also, you'll need to download a few pre-release packages and tell NuGet where to
find them.  Set `platform=linux-arm64`, `platform=linux-x64`,
`platform=win-x64`, or `platform=osx-arm64` and run:

```
mkdir packages
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.wasi-wasm.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
curl -LO --output-dir packages https://github.com/dicej/spin-dotnet-sdk/releases/download/canary/runtime.$platform.Microsoft.DotNet.ILCompiler.LLVM.9.0.0-dev.nupkg
export NUGET_LOCAL_PATH=$(pwd)/packages
```

## Building and Running

```
dotnet publish
cargo run --release --manifest-path host/Cargo.toml bin/Release/net9.0/wasi-wasm/publish/csharp-wasm.wasm bytecodealliance.org:443
```

## Debugging

```
dotnet publish -c Debug
cargo build --release --manifest-path host/Cargo.toml
gdb --args ./host/target/release/host --debug bin/Debug/net9.0/wasi-wasm/publish/csharp-wasm.wasm bytecodealliance.org:443
```

Once in `gdb` you can set breakpoints (e.g. `break
S_P_CoreLib_System_Runtime_EH__RhpThrowEx` to break when an exception is
thrown), step by instruction, etc.
