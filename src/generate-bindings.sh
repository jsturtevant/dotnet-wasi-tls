#!/bin/sh

set -ex

cargo install --locked --no-default-features --features csharp wit-bindgen-cli --version 0.32.0 --root $(pwd)
./bin/wit-bindgen c-sharp --features tls -w imports -r native-aot wasi-sockets/wit
rm ImportsWorld_wasm_import_linkage_attribute.cs
