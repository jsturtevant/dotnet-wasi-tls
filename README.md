# .NET `SslStream` on WASI experiment

This doesn't work at all yet.  Nothing to see here, folks.

TODO: document prerequisites, etc.

You'll need to update nuget.config and/or App.csproj to change the hard-coded filesystem path and platform.

```
dotnet publish
wasmtime run -S allow-ip-name-lookup -S inherit-network -S http bin/Release/net9.0/wasi-wasm/publish/csharp-wasm.wasm bytecodealliance.org:443
```
