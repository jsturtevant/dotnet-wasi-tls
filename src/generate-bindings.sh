#!/bin/sh

set -ex

# todo this isn't automated fully
cargo install --locked --no-default-features --features csharp wit-bindgen-cli --version 0.39.0 --root $(pwd)
./bin/wit-bindgen c-sharp -w imports -r native-aot ../wasi-sockets/wit 
./bin/wit-bindgen c-sharp --features tls -w imports -r native-aot --out-dir ./tls ../wasi-tls/wit 
rm ImportsWorld_wasm_import_linkage_attribute.cs
