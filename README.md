# .NET `SslStream` on WASI experiment

This demonstrates the feasibility of implementing a subset of .NET's
`System.Net.Security.SslStream` based on [a proposed `wasi:tls`](https://github.com/WebAssembly/wasi-tls)

## Prequisites

- .NET 9 
- wasmtime built on https://github.com/bytecodealliance/wasmtime/pull/10249 or `curl -LO https://github.com/jsturtevant/wasmtime/releases/download/wasi-tls-demo/wasmtime`

## Building and Running

```
dotnet publish App.csproj
wasmtime -S inherit-network -S cli -S tcp -S allow-ip-name-lookup -S tls bin/Release/net9.0/wasi-wasm/publish/Wasi.Tls.wasm bytecodealliance.org:443
```

## Debugging

```
dotnet publish App.csproj -c Debug
cargo build --release --manifest-path host/Cargo.toml
gdb --args wasmtime -S inherit-network -S cli -S tcp -S allow-ip-name-lookup -S tls --debug bin/Debug/net9.0/wasi-wasm/publish/Wasi.Tls.wasm bytecodealliance.org:443
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
