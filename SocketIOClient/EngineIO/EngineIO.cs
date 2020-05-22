using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SocketIOClient.Arguments;
using SocketIOClient.Parsers;
namespace EngineIOClient {
    public class EngineIO : Events.Events {

        protected const int ReceiveChunkSize = 1024;
        protected const int SendChunkSize = 1024;

        protected readonly Uri _uri;
        protected ClientWebSocket _socket;
        protected readonly UrlConverter _urlConverter;
        protected readonly string _namespace;
        protected CancellationTokenSource _tokenSource;
        protected int _packetId;
        public bool _isHeartbeatFinished = true;
        public bool _isConnectTimeout = false;
        public int _timeoutNumber = 0;
        public int _heartbeatDelay = 0;
        protected int _heartbeatTimeoutDelay = 2000;
        protected int _timeoutTimesForClose;
        public EngineIO (Uri uri, Option opt) {
            _heartbeatTimeoutDelay = opt.HeartbeatTimeoutDelay;
            _timeoutTimesForClose = opt.TimeoutTimesForClose;
            if (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "wss" || uri.Scheme == "ws") {
                _uri = uri;
            } else {
                throw new ArgumentException ("Unsupported protocol");
            }
            _urlConverter = new UrlConverter ();
            if (_uri.AbsolutePath != "/") {
                _namespace = _uri.AbsolutePath + ',';
            }
            _packetId = -1;
        }
    }
}