use {
    crate::bindings::wasi::sockets::tls::HostFutureStreams, anyhow::anyhow, async_trait::async_trait, bindings::wasi::sockets::tls::{self, AlpnId, CipherSuite, PrivateIdentity, ProtocolVersion}, bytes::Bytes, clap::Parser, futures::{channel::mpsc, SinkExt, StreamExt}, ::native_tls::Certificate, std::{
        any::Any,
        future::Future,
        mem,
        path::PathBuf,
        pin::Pin,
        task::{ready, Context, Poll},
    }, tokio::io::{AsyncRead, AsyncReadExt, AsyncWrite, ReadBuf}, tokio_native_tls::{native_tls::{self, TlsStream as nativestream}, TlsConnector, TlsStream}, wasmtime::{
        component::{Component, Linker, Resource, ResourceTable},
        Config, Engine, OptLevel, Store,
    }, wasmtime_wasi::{
        bindings::{
            io::{
                poll::Pollable,
                streams::{InputStream, OutputStream},
            },
            Command,
        },
        pipe::{AsyncReadStream, AsyncWriteStream},
        HostInputStream, HostOutputStream, StreamError, StreamResult, Subscribe, WasiCtx,
        WasiCtxBuilder, WasiView,
    }, wasmtime_wasi_http::{WasiHttpCtx, WasiHttpView}
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
            "wasi:sockets/tls/public-identity": super::PublicIdentity
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

    // accept invalid hostnames
    danger_accept_invalid_hostnames: bool,

    // accept invalid certs
    danger_accept_invalid_certs: bool,

    /// Grant access of a host directory to a guest.
    ///
    /// If specified as just `HOST_DIR` then the same directory name on the
    /// host is made available within the guest. If specified as `HOST::GUEST`
    /// then the `HOST` directory is opened and made available as the name
    /// `GUEST` in the guest.
    #[arg(long = "dir", value_name = "HOST_DIR[::GUEST_DIR]", value_parser = parse_dirs)]
    pub dirs: Vec<(String, String)>,
}

fn parse_dirs(s: &str) -> anyhow::Result<(String, String)> {
    let mut parts = s.split("::");
    let host = parts.next().unwrap();
    let guest = match parts.next() {
        Some(guest) => guest,
        None => host,
    };
    Ok((host.into(), guest.into()))
}

struct Ctx {
    table: ResourceTable,
    wasi: WasiCtx,
    http: WasiHttpCtx,
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

impl WasiHttpView for Ctx {
    fn ctx(&mut self) -> &mut WasiHttpCtx {
        &mut self.http
    }
    fn table(&mut self) -> &mut ResourceTable {
        &mut self.table
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
                Promise::None => todo!("cancel safety"),
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
                Promise::None => todo!("cancel safety"),
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

pub struct ClientConnection{
    streams: Option<Streams>, 
    connection_info: Option<ConnectionInfo>,
}

pub struct PublicIdentity{
    peer_cert: Option<Certificate>
}

// pub struct PrivateIdentity{}
// pub struct PublicIdentity{}

// impl HostPrivateIdentity for HostPrivateIdentity {
//     fn public_identity(&mut self,self_:wasmtime::component::Resource<PrivateIdentity> ,) -> wasmtime::Result<wasmtime::component::Resource<PublicIdentity> > {
//         todo!()
//     }
// }

// impl HostPublicIdentity for PublicIdentity{
//     fn public_key(&mut self,self_:wasmtime::component::Resource<PublicIdentity> ,) -> wasmtime::Result<Option<wasmtime::component::Resource<Bytes> > >  {
//         todo!()
//     }
// }

pub struct ClientHandshake {
    streams: Streams,
    host: String,
    client_id: u32,
}

pub enum FutureStreams {
    Pending(mpsc::Receiver<Result<TlsStream<Streams>, native_tls::Error>>, u32),
    Ready(Option<Result<TlsStream<Streams>, native_tls::Error>>, u32),
    Gone,
}

#[derive(Clone)]
struct ConnectionInfo {
    negotiatedAlpn: Option<Vec<u8>>,
    peer_cert: Option<Certificate>
}

#[async_trait]
impl Subscribe for FutureStreams {
    async fn ready(&mut self) {
        if let Self::Pending(receiver, client_id) = self {
            let value = receiver.next().await;
            *self = Self::Ready(value, *client_id);
        }
    }
}

struct ReceiverInputStream {
    input: mpsc::Receiver<Bytes>,
    buffer: Option<StreamResult<Bytes>>,
}

impl HostInputStream for ReceiverInputStream {
    fn read(&mut self, size: usize) -> StreamResult<Bytes> {
        let mut bytes = self.buffer.take().unwrap_or_else(|| Ok(Bytes::new()))?;
        if bytes.len() > size {
            
            self.buffer = Some(Ok(bytes.split_off(size)));
        }
        Ok(bytes)
    }
}

#[async_trait]
impl Subscribe for ReceiverInputStream {
    async fn ready(&mut self) {
        if self.buffer.is_none() {
            if let Some(bytes) = self.input.next().await {
                
                self.buffer = Some(Ok(bytes))
            } else {
                self.buffer = Some(Err(StreamError::Closed));
            }
        }
    }
}

struct SenderOutputStream {
    output: mpsc::Sender<Bytes>,
    buffer: Option<StreamResult<Bytes>>,
}

impl HostOutputStream for SenderOutputStream {
    fn write(&mut self, bytes: Bytes) -> StreamResult<()> {
        match &self.buffer {
            None => {
                if !bytes.is_empty() {
                    
                    self.buffer = Some(Ok(bytes));
                }
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
            None => Ok(1024 * 1024),
            Some(Ok(_)) => Ok(0),
            Some(Err(_)) => self.buffer.take().unwrap().map(|_| unreachable!()),
        }
    }
}

#[async_trait]
impl Subscribe for SenderOutputStream {
    async fn ready(&mut self) {
        if let Some(Ok(_)) = &self.buffer {
            let Some(Ok(bytes)) = self.buffer.take() else {
                unreachable!();
            };
            
            match self.output.send(bytes).await {
                Ok(()) => {
                    self.buffer = None;
                }
                Err(_) => {
                    self.buffer = Some(Err(StreamError::Closed));
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
        Ok(self.table.push(ClientConnection {
            streams: Some(Streams {
                input: Promise::Ready(input),
                output: Promise::Ready(output),
            }),
            connection_info: None,
        })?)
    }

    fn connect(
        &mut self,
        this: Resource<ClientConnection>,
        host: String,
    ) -> wasmtime::Result<Result<Resource<ClientHandshake>, ()>> {
        if let Some(streams) = self.table.get_mut(&this)?.streams.take() {
            
            Ok(Ok(self.table.push(ClientHandshake { streams, host, client_id: this.rep() })?))
        } else {
            Ok(Err(()))
        }
    }

    fn drop(&mut self, this: Resource<ClientConnection>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
    
    fn server_name(&mut self, this: Resource<ClientConnection> ,) -> wasmtime::Result<Option<String> >  {
        todo!()
    }
    
    fn alpn_id(&mut self,this:Resource<ClientConnection> ,) -> wasmtime::Result<Option<AlpnId> >  {
        let conn = self.table.get(&this)?;
        match &conn.connection_info {
            Some(conn) =>{
                
                Ok(conn.negotiatedAlpn.clone())
            }
            None => Ok(None)
        }
    }
    
    fn client_identity(&mut self,this:Resource<ClientConnection> ,) -> wasmtime::Result<Option<Resource<PrivateIdentity> > >  {
        todo!()
    }
    
    fn server_identity(&mut self,this:Resource<ClientConnection> ,) -> wasmtime::Result<Option<Resource<PublicIdentity> > >  {
        let conn = self.table.get(&this)?;
        match &conn.connection_info {
            Some(conn) =>{
                
             Ok(Some(self.table.push(PublicIdentity{
                    peer_cert: conn.peer_cert.clone()
                })?))
            }
            None => {
                
                Ok(None)
            }
        }
    }
    
    fn protocol_version(&mut self,this:Resource<ClientConnection> ,) -> wasmtime::Result<Option<ProtocolVersion> >  {
        todo!()
    }

    fn cipher_suite(&mut self,this:Resource<ClientConnection> ,) -> wasmtime::Result<Option<CipherSuite> >  {
        todo!()
    }
}

impl tls::HostPublicIdentity for Ctx {
    fn export_x509_chain(&mut self, this:Resource<PublicIdentity> ,) -> wasmtime::Result<Vec<Vec<u8> > >  {
       let id = self.table.get(&this)?;
       match &id.peer_cert {
        Some(cert) =>{
            Ok(vec![cert.to_der()?])
        }
        None => Ok(vec![vec![]])
       }
    }

    fn drop(&mut self, this:Resource<PublicIdentity>) -> wasmtime::Result<()>  {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::HostPrivateIdentity for Ctx {
    fn public_identity(&mut self,self_:wasmtime::component::Resource<PrivateIdentity> ,) -> wasmtime::Result<wasmtime::component::Resource<PublicIdentity> >  {
        todo!()
    }

    fn drop(&mut self,rep:wasmtime::component::Resource<PrivateIdentity>) -> wasmtime::Result<()>  {
        todo!()
    }
}

impl tls::HostClientHandshake for Ctx {
    fn finish(
        &mut self,
        this: Resource<ClientHandshake>,
    ) -> wasmtime::Result<Resource<FutureStreams>> {
        let handshake = self.table.delete(this)?;
        let connector = self.connector.clone();
        let (mut tx, rx) = mpsc::channel(1);
        tokio::task::spawn(async move {
            
            _ = tx
                .send(connector.connect(&handshake.host, handshake.streams).await)
                .await;
        });
        Ok(self.table.push(FutureStreams::Pending(rx, handshake.client_id))?)
    }

    fn drop(&mut self, this: Resource<ClientHandshake>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::HostFutureStreams for Ctx {
    fn subscribe(&mut self, this: Resource<FutureStreams>) -> wasmtime::Result<Resource<Pollable>> {
        wasmtime_wasi::subscribe(WasiView::table(self), this)
    }

    fn get(
        &mut self,
        this: Resource<FutureStreams>,
    ) -> wasmtime::Result<
        Option<Result<Result<(Resource<InputStream>, Resource<OutputStream>), ()>, ()>>,
    > {
        {
            let this = self.table.get(&this)?;
            match this {
                FutureStreams::Pending(_, _) => return Ok(None),
                FutureStreams::Ready(None, _) | FutureStreams::Ready(Some(Err(_)), _) => {
                    return Ok(Some(Ok(Err(()))));
                }
                FutureStreams::Ready(Some(Ok(_)), _) => (),
                FutureStreams::Gone => return Ok(Some(Err(()))),
            }
        }

        let FutureStreams::Ready(Some(Ok(stream)), client_id) =
            mem::replace(self.table.get_mut(&this)?, FutureStreams::Gone)
        else {
            unreachable!()
        };

        if let Some(conn) = self.table.get_any_mut(client_id)?.downcast_mut::<ClientConnection>() {
            
            let conn_info = ConnectionInfo {
                negotiatedAlpn: stream.get_ref().negotiated_alpn()?,
                peer_cert: stream.get_ref().peer_certificate()?,
            };
            
            
            conn.connection_info = Some(conn_info);
        }

        let (rx, tx) = tokio::io::split(stream);

        let rx = self
            .table
            .push(InputStream::Host(Box::new(AsyncReadStream::new(rx))))?;

        let tx = self
            .table
            .push(Box::new(AsyncWriteStream::new(64 * 1024, tx)) as OutputStream)?;

        Ok(Some(Ok(Ok((rx, tx)))))
    }

    fn drop(&mut self, this: Resource<FutureStreams>) -> wasmtime::Result<()> {
        self.table.delete(this)?;
        Ok(())
    }
}

impl tls::Host for Ctx {
    fn make_pipe(&mut self) -> wasmtime::Result<(Resource<InputStream>, Resource<OutputStream>)> {
        
        let (tx, rx) = mpsc::channel(1);

        let rx = self
            .table
            .push(InputStream::Host(Box::new(ReceiverInputStream {
                input: rx,
                buffer: None,
            })))?;

        let tx = self.table.push(Box::new(SenderOutputStream {
            output: tx,
            buffer: None,
        }) as OutputStream)?;

        Ok((rx, tx))
    }
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    pretty_env_logger::init();

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
    wasmtime_wasi_http::add_only_http_to_linker_sync(&mut linker)?;

    let mut wasi = WasiCtxBuilder::new();
    wasi.inherit_stdio()
        .inherit_network()
        .allow_ip_name_lookup(true)
        .arg("command");

    for (host, guest) in options.dirs.iter() {
        wasi.preopened_dir(
            host,
            guest,
            wasmtime_wasi::DirPerms::all(),
            wasmtime_wasi::FilePerms::all(),
        )?;
    }

    for arg in &options.args {
        wasi.arg(arg);
    }

    let mut conn_builder = native_tls::TlsConnector::builder();

    if options.danger_accept_invalid_hostnames {
        conn_builder.danger_accept_invalid_hostnames(true);
    }

    if options.danger_accept_invalid_certs {
        conn_builder.danger_accept_invalid_certs(true);
    }

    let mut store = Store::new(
        &engine,
        Ctx {
            table: ResourceTable::new(),
            wasi: wasi.build(),
            connector: TlsConnector::from(conn_builder.build()?),
            http: WasiHttpCtx::new(),
        },
    );

    Command::instantiate_async(&mut store, &component, &linker)
        .await?
        .wasi_cli_run()
        .call_run(&mut store)
        .await?
        .map_err(|()| anyhow::anyhow!("command returned with failing exit status"))
}
