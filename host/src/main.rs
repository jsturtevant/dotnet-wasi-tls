use {
    anyhow::anyhow,
    async_trait::async_trait,
    bindings::wasi::sockets::tls,
    bytes::{Bytes, BytesMut},
    clap::Parser,
    std::{
        future::Future,
        mem,
        path::PathBuf,
        pin::Pin,
        task::{ready, Context, Poll},
    },
    tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt, ReadBuf},
    tokio_native_tls::{native_tls, TlsConnector, TlsStream},
    wasmtime::{
        component::{Component, Linker, Resource, ResourceTable},
        Config, Engine, OptLevel, Store,
    },
    wasmtime_wasi::{
        bindings::{
            io::{
                poll::Pollable,
                streams::{InputStream, OutputStream},
            },
            Command,
        },
        HostInputStream, HostOutputStream, StreamError, StreamResult, Subscribe, WasiCtx,
        WasiCtxBuilder, WasiView,
    },
};

mod bindings {
    wasmtime::component::bindgen!({
        world: "imports",
        path: "../wasi-sockets/wit",
        features: ["tls"],
        with: {
            "wasi:io": wasmtime_wasi::bindings::io,
            "wasi:sockets/tls/client-connection": super::ClientConnection,
            "wasi:sockets/tls/client-handshake": super::ClientHandshake,
            "wasi:sockets/tls/future-streams": super::FutureStreams,
        },
        trappable_imports: true,
        async: {
            only_imports: [],
        }
    });
}

#[derive(Parser)]
struct Options {
    /// Component to run.
    ///
    /// The component should export the `wasi:cli/run` interface.
    component: PathBuf,

    /// If specified, emit debug info and disable optimization.
    #[arg(short, long)]
    debug: bool,

    /// Arguments to pass to the component.
    #[arg(trailing_var_arg = true, allow_hyphen_values = true)]
    args: Vec<String>,
}

struct Ctx {
    table: ResourceTable,
    wasi: WasiCtx,
    connector: TlsConnector,
}

impl WasiView for Ctx {
    fn table(&mut self) -> &mut ResourceTable {
        &mut self.table
    }
    fn ctx(&mut self) -> &mut WasiCtx {
        &mut self.wasi
    }
}

enum Promise<T> {
    Ready(T),
    Pending(Pin<Box<dyn Future<Output = T> + Send>>),
    None,
}

pub struct Streams {
    input: Promise<Box<dyn HostInputStream>>,
    output: Promise<Box<dyn HostOutputStream>>,
}

impl AsyncRead for Streams {
    fn poll_read(
        mut self: Pin<&mut Self>,
        cx: &mut Context,
        buf: &mut ReadBuf,
    ) -> Poll<std::io::Result<()>> {
        loop {
            match &mut self.as_mut().input {
                Promise::None => unreachable!(),
                Promise::Pending(future) => {
                    let value = ready!(future.as_mut().poll(cx));
                    self.as_mut().input = Promise::Ready(value);
                }
                Promise::Ready(input) => {
                    match input.read(buf.remaining()) {
                        Ok(bytes) => {
                            if bytes.is_empty() {
                                let Promise::Ready(mut input) =
                                    mem::replace(&mut self.as_mut().input, Promise::None)
                                else {
                                    unreachable!()
                                };
                                self.as_mut().input = Promise::Pending(Box::pin(async move {
                                    input.ready().await;
                                    input
                                }));
                            } else {
                                buf.put_slice(&bytes);
                                return Poll::Ready(Ok(()));
                            }
                        }
                        Err(StreamError::Closed) => return Poll::Ready(Ok(())),
                        Err(StreamError::LastOperationFailed(e) | StreamError::Trap(e)) => {
                            return Poll::Ready(Err(std::io::Error::other(e)))
                        }
                    };
                }
            }
        }
    }
}

impl AsyncWrite for Streams {
    fn poll_write(
        mut self: Pin<&mut Self>,
        cx: &mut Context,
        buf: &[u8],
    ) -> Poll<std::io::Result<usize>> {
        loop {
            match &mut self.as_mut().output {
                Promise::None => unreachable!(),
                Promise::Pending(future) => {
                    let value = ready!(future.as_mut().poll(cx));
                    self.as_mut().output = Promise::Ready(value);
                }
                Promise::Ready(output) => {
                    match output.check_write() {
                        Ok(0) => {
                            let Promise::Ready(mut output) =
                                mem::replace(&mut self.as_mut().output, Promise::None)
                            else {
                                unreachable!()
                            };
                            self.as_mut().output = Promise::Pending(Box::pin(async move {
                                output.ready().await;
                                output
                            }));
                        }
                        Ok(count) => {
                            let count = count.min(buf.len());
                            return match output.write(Bytes::copy_from_slice(&buf[..count])) {
                                Ok(()) => Poll::Ready(Ok(count)),
                                Err(StreamError::Closed) => Poll::Ready(Ok(0)),
                                Err(StreamError::LastOperationFailed(e) | StreamError::Trap(e)) => {
                                    Poll::Ready(Err(std::io::Error::other(e)))
                                }
                            };
                        }
                        Err(StreamError::Closed) => return Poll::Ready(Ok(0)),
                        Err(StreamError::LastOperationFailed(e) | StreamError::Trap(e)) => {
                            return Poll::Ready(Err(std::io::Error::other(e)))
                        }
                    };
                }
            }
        }
    }

    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context) -> Poll<std::io::Result<()>> {
        self.poll_write(cx, &[]).map(|v| v.map(drop))
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context) -> Poll<std::io::Result<()>> {
        self.poll_flush(cx)
    }
}

pub struct ClientConnection(Option<Streams>);

pub struct ClientHandshake {
    streams: Streams,
    host: String,
}

pub struct FutureStreams(Promise<Result<TlsStream<Streams>, native_tls::Error>>);

#[async_trait]
impl Subscribe for FutureStreams {
    async fn ready(&mut self) {
        match &self.0 {
            Promise::Pending(_) => (),
            Promise::Ready(_) | Promise::None => return,
        }

        let Promise::Pending(future) = mem::replace(&mut self.0, Promise::None) else {
            unreachable!()
        };
        self.0 = Promise::Ready(future.await);
    }
}

struct TlsInputStream {
    input: tokio::io::ReadHalf<TlsStream<Streams>>,
    buffer: Option<StreamResult<Bytes>>,
}

impl HostInputStream for TlsInputStream {
    fn read(&mut self, size: usize) -> StreamResult<Bytes> {
        let mut bytes = self.buffer.take().unwrap_or_else(|| Ok(Bytes::new()))?;
        if bytes.len() > size {
            self.buffer = Some(Ok(bytes.split_off(size)));
        }
        Ok(bytes)
    }
}

#[async_trait]
impl Subscribe for TlsInputStream {
    async fn ready(&mut self) {
        if self.buffer.is_none() {
            let mut buf = BytesMut::with_capacity(64 * 1024);
            match self.input.read_buf(&mut buf).await {
                Ok(0) => {
                    self.buffer = Some(Err(StreamError::Closed));
                }
                Ok(count) => {
                    buf.truncate(count);
                    self.buffer = Some(Ok(buf.freeze()))
                }
                Err(e) => {
                    self.buffer = Some(Err(StreamError::LastOperationFailed(anyhow!(e))));
                }
            }
        }
    }
}

struct TlsOutputStream {
    output: tokio::io::WriteHalf<TlsStream<Streams>>,
    buffer: Option<StreamResult<Bytes>>,
}

impl HostOutputStream for TlsOutputStream {
    fn write(&mut self, bytes: Bytes) -> StreamResult<()> {
        match &self.buffer {
            None => {
                self.buffer = Some(Ok(bytes));
                Ok(())
            }
            Some(Ok(_)) => Err(StreamError::LastOperationFailed(anyhow!(
                "write buffer full"
            ))),
            Some(Err(_)) => self.buffer.take().unwrap().map(|_| unreachable!()),
        }
    }

    fn flush(&mut self) -> StreamResult<()> {
        match &self.buffer {
            None => Ok(()),
            Some(Ok(_)) => Err(StreamError::LastOperationFailed(anyhow!(
                "write buffer full"
            ))),
            Some(Err(_)) => self.buffer.take().unwrap().map(|_| unreachable!()),
        }
    }

    fn check_write(&mut self) -> StreamResult<usize> {
        match &self.buffer {
            None => Ok(64 * 1024),
            Some(Ok(_)) => Ok(0),
            Some(Err(_)) => self.buffer.take().unwrap().map(|_| unreachable!()),
        }
    }
}

#[async_trait]
impl Subscribe for TlsOutputStream {
    async fn ready(&mut self) {
        while let Some(Ok(bytes)) = &mut self.buffer {
            match self.output.write(bytes).await {
                Ok(0) => {
                    self.buffer = Some(Err(StreamError::Closed));
                }
                Ok(count) => {
                    _ = bytes.split_to(count);

                    if bytes.is_empty() {
                        self.buffer = None;
                    }
                }
                Err(e) => {
                    self.buffer = Some(Err(StreamError::LastOperationFailed(anyhow!(e))));
                }
            }
        }
    }
}

impl tls::HostClientConnection for Ctx {
    fn new(
        &mut self,
        input: Resource<InputStream>,
        output: Resource<OutputStream>,
    ) -> wasmtime::Result<Resource<ClientConnection>> {
        let InputStream::Host(input) = self.table.delete(input)? else {
            return Err(anyhow!("file input streams not yet supported"));
        };
        let output = self.table.delete(output)?;
        Ok(self.table.push(ClientConnection(Some(Streams {
            input: Promise::Ready(input),
            output: Promise::Ready(output),
        })))?)
    }

    fn connect(
        &mut self,
        this: Resource<ClientConnection>,
        host: String,
    ) -> wasmtime::Result<Result<Resource<ClientHandshake>, ()>> {
        if let Some(streams) = self.table.get_mut(&this)?.0.take() {
            Ok(Ok(self.table.push(ClientHandshake { streams, host })?))
        } else {
            Ok(Err(()))
        }
    }

    fn drop(&mut self, this: Resource<ClientConnection>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::HostClientHandshake for Ctx {
    fn finish(
        &mut self,
        this: Resource<ClientHandshake>,
    ) -> wasmtime::Result<Resource<FutureStreams>> {
        let handshake = self.table.delete(this)?;
        let connector = self.connector.clone();
        Ok(self
            .table
            .push(FutureStreams(Promise::Pending(Box::pin(async move {
                connector.connect(&handshake.host, handshake.streams).await
            }))))?)
    }

    fn drop(&mut self, this: Resource<ClientHandshake>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::HostFutureStreams for Ctx {
    fn subscribe(&mut self, this: Resource<FutureStreams>) -> wasmtime::Result<Resource<Pollable>> {
        wasmtime_wasi::subscribe(self.table(), this)
    }

    fn get(
        &mut self,
        this: Resource<FutureStreams>,
    ) -> wasmtime::Result<
        Option<Result<Result<(Resource<InputStream>, Resource<OutputStream>), ()>, ()>>,
    > {
        {
            let this = self.table.get(&this)?;
            match &this.0 {
                Promise::Pending(_) => return Ok(None),
                Promise::Ready(Ok(_)) => (),
                Promise::Ready(Err(_)) => return Ok(Some(Ok(Err(())))),
                Promise::None => return Ok(Some(Err(()))),
            }
        }

        let Promise::Ready(Ok(stream)) =
            mem::replace(&mut self.table.get_mut(&this)?.0, Promise::None)
        else {
            unreachable!()
        };

        let (rx, tx) = tokio::io::split(stream);

        let rx = self.table.push(InputStream::Host(Box::new(TlsInputStream {
            input: rx,
            buffer: None,
        })))?;

        let tx = self.table.push(Box::new(TlsOutputStream {
            output: tx,
            buffer: None,
        }) as OutputStream)?;

        Ok(Some(Ok(Ok((rx, tx)))))
    }

    fn drop(&mut self, this: Resource<FutureStreams>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::Host for Ctx {}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let options = Options::parse();

    let mut config = Config::new();
    config.wasm_component_model(true);
    config.async_support(true);

    if options.debug {
        config.debug_info(true);
        config.cranelift_opt_level(OptLevel::None);
    }

    let engine = Engine::new(&config)?;

    let component = Component::from_file(&engine, &options.component)?;

    let mut linker = Linker::new(&engine);

    wasmtime_wasi::add_to_linker_async(&mut linker)?;
    tls::add_to_linker(&mut linker, |ctx| ctx)?;

    let mut wasi = WasiCtxBuilder::new();
    wasi.inherit_stdio()
        .inherit_network()
        .allow_ip_name_lookup(true)
        .arg("command");

    for arg in &options.args {
        wasi.arg(arg);
    }

    let mut store = Store::new(
        &engine,
        Ctx {
            table: ResourceTable::new(),
            wasi: wasi.build(),
            connector: TlsConnector::from(native_tls::TlsConnector::new()?),
        },
    );

    Command::instantiate_async(&mut store, &component, &linker)
        .await?
        .wasi_cli_run()
        .call_run(&mut store)
        .await?
        .map_err(|()| anyhow::anyhow!("command returned with failing exit status"))
}
