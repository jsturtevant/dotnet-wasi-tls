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
dotnet publish App.csproj
cargo run --release --manifest-path host/Cargo.toml bin/Release/net9.0/wasi-wasm/publish/Wasi.Tls.wasm bytecodealliance.org:443
```

## Debugging

```
dotnet publish App.csproj -c Debug
cargo build --release --manifest-path host/Cargo.toml
gdb --args ./host/target/release/host --debug bin/Debug/net9.0/wasi-wasm/publish/Wasi.Tls.wasm bytecodealliance.org:443
```

Once in `gdb` you can set breakpoints (e.g. `break
S_P_CoreLib_System_Runtime_EH__RhpThrowEx` to break when an exception is
thrown), step by instruction, etc.

## generate a package

```
dotnet pack library.csproj 
```

To use the package: 

Copy to local dir `cp bin/Release/Wasi.Tls.0.0.1.nupkg $(NUGET_LOCAL_PATH)`
Then add `<add key="Wasi.Tls" value="%NUGET_LOCAL_PATH%" />` to the `nuget.config` file for the project

Add a package reference to the project:

```
<PackageReference Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))" Include="Wasi.Tls" Version="0.0.1" />
```
