﻿using System;
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

namespace SocketIOClient {
    public class SocketIO : EngineIOClient.EngineIO {
        public SocketIO (Uri uri, Option opt) : base (uri, opt) {
            EventHandlers = new Dictionary<string, EventHandler> ();
            Callbacks = new Dictionary<int, EventHandler> ();
            ConnectTimeout = TimeSpan.FromSeconds (10);
        }

        public SocketIO (string uri) : this (new Uri (uri), new Option ()) { }
        public Dictionary<int, EventHandler> Callbacks { get; }

        public int EIO { get; set; } = 3;
        public string Path { get; set; }
        public TimeSpan ConnectTimeout { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public event Action OnConnected;
        public event Action<ResponseArgs> OnError;
        public event Action<ServerCloseReason> OnClosed;

        public event Action OnPing;

        public event Action<string> OnPong;

        public event Action<string> OnConnectError;

        public event Action<string> OnConnectTimeout;

        public event Action<string> OnReconnect;

        public event Action<string> OnReconnectAttempt;

        public event Action<string> OnReconnecting;

        public event Action<string> OnReconnectError;

        public event Action<string> OnReconnectFailed;

        public event Action<string, ResponseArgs> UnhandledEvent;
        public event Action<string, ResponseArgs> OnReceivedEvent;

        public Dictionary<string, EventHandler> EventHandlers { get; }

        public SocketIOState State { get; private set; }

        public Task ConnectAsync () {
            var token = new CancellationTokenSource (ConnectTimeout).Token;
            _tokenSource = new CancellationTokenSource ();
            Uri wsUri = _urlConverter.HttpToWs (_uri, EIO.ToString (), Path, Parameters);
            if (_socket != null) {
                _socket.Dispose ();
            }
            _socket = new ClientWebSocket ();
            Task connectionTask;
            bool executed = false;
            try {
                connectionTask = _socket.ConnectAsync (wsUri, _tokenSource.Token);
                executed = connectionTask.Wait (TimeSpan.FromSeconds (1));
            } catch (System.Exception) {
                InvokeConnectError ();
                return Task.CompletedTask;
            }
            if (!executed) {
                throw new TimeoutException ();
            }
            Listen ();
            return Task.CompletedTask;
        }

        public Task CloseAsync () {
            if (_socket == null) {
                throw new InvalidOperationException ("Close failed, must connect first.");
            } else {
                _tokenSource.Cancel ();
                _tokenSource.Dispose ();
                _socket.Abort ();
                _socket.Dispose ();
                _socket = null;
                OnClosed?.Invoke (ServerCloseReason.ClosedByClient);
                return Task.CompletedTask;
            }
        }

        private void Listen () {
            // Listen State
            Task.Factory.StartNew (async () => {
                while (true) {
                    await Task.Delay (500);
                    if (!_isHeartbeatFinished) {
                        _heartbeatDelay += 500;
                    }
                    if (!_isConnectTimeout && _heartbeatDelay >= _heartbeatTimeoutDelay) {
                        _isConnectTimeout = true;
                        if (_timeoutNumber == 0) {
                            InvokeConnectTimeout ();
                        }
                        _timeoutNumber++;
                        if (_timeoutNumber == 2) {
                            await CloseAsync ();
                        }
                    }
                    if (_socket.State == WebSocketState.Aborted || _socket.State == WebSocketState.Closed) {
                        if (State != SocketIOState.Closed) {
                            State = SocketIOState.Closed;
                            _tokenSource.Cancel ();
                            OnClosed?.Invoke (ServerCloseReason.Aborted);
                        }
                    }
                }
            }, _tokenSource.Token);

            // Listen Message
            Task.Factory.StartNew (async () => {
                while (true) {
                    var buffer = new byte[ReceiveChunkSize];
                    Console.WriteLine ("_socket.State " + _socket.State.ToString ());
                    if (_socket.State == WebSocketState.Open) {
                        WebSocketReceiveResult result = await _socket.ReceiveAsync (new ArraySegment<byte> (buffer), _tokenSource.Token);
                        if (result.MessageType == WebSocketMessageType.Text) {
                            var bufferList = new List<byte> ();
                            byte[] bufferTotal;
                            int totalCount = 0;
                            bufferList.AddRange (buffer);
                            totalCount += result.Count;

                            while (!result.EndOfMessage) {
                                result = await _socket.ReceiveAsync (new ArraySegment<byte> (buffer), _tokenSource.Token);
                                totalCount += result.Count;
                                bufferList.AddRange (buffer);
                            }
                            bufferTotal = bufferList.ToArray ();
                            Console.WriteLine ($"GetWebSocket Data in len: {bufferList.Count()}, data: {Encoding.UTF8.GetString(bufferTotal, 0, totalCount)}");
                            var parser = new ResponseTextParser (_namespace, this) {
                                Text = Encoding.UTF8.GetString (bufferTotal, 0, totalCount)
                            };
                            //Console.WriteLine("parser.ParseAsync" + parser.Text);
                            await parser.ParseAsync ();
                        } else if (result.MessageType == WebSocketMessageType.Binary) {
                            string str = "binary";
                            try {
                                str = Encoding.UTF8.GetString (buffer, 0, result.Count);
                            } catch (Exception e) {
                                Console.WriteLine (e.ToString ());
                            }
                            Console.WriteLine ("GetWebSocket Data : " + str);
                        }
                    } else {
                        Thread.Sleep (1000);
                    }
                }
            }, _tokenSource.Token);
        }

        private async Task SendMessageAsync (string text) {
            if (_socket.State == WebSocketState.Open) {
                var messageBuffer = Encoding.UTF8.GetBytes (text);
                var messagesCount = (int) Math.Ceiling ((double) messageBuffer.Length / SendChunkSize);

                for (var i = 0; i < messagesCount; i++) {
                    int offset = SendChunkSize * i;
                    int count = SendChunkSize;
                    bool isEndOfMessage = (i + 1) == messagesCount;

                    if ((count * (i + 1)) > messageBuffer.Length) {
                        count = messageBuffer.Length - offset;
                    }
                    await _socket.SendAsync (new ArraySegment<byte> (messageBuffer, offset, count), WebSocketMessageType.Text, isEndOfMessage, _tokenSource.Token);
                }
            }
        }

        public Task InvokeConnectedAsync () {
            State = SocketIOState.Connected;
            OnConnected?.Invoke ();
            return Task.CompletedTask;
        }

        public async Task InvokeClosedAsync () {
            if (State != SocketIOState.Closed) {
                State = SocketIOState.Closed;
                await _socket.CloseAsync (WebSocketCloseStatus.NormalClosure, string.Empty, _tokenSource.Token);
                _tokenSource.Cancel ();
                Console.WriteLine ("_tokenSource canceled");
                OnClosed?.Invoke (ServerCloseReason.ClosedByServer);
            }
        }

        public async Task InvokeOpenedAsync (OpenedArgs args) {
            await Task.Factory.StartNew (async () => {
                if (_namespace != null) {
                    await SendMessageAsync ("40" + _namespace);
                }
                State = SocketIOState.Connected;
                while (true) {
                    if (State == SocketIOState.Connected) {
                        await Task.Delay (args.PingInterval);
                        SendMessageAsync (((int) EngineIOProtocol.Ping).ToString ());
                        _isHeartbeatFinished = false;
                        _heartbeatDelay = 0;
                        _isConnectTimeout = false;
                    } else {
                        break;
                    }
                }
            });
        }

        public void InvokePingAsync () {
            OnPing?.Invoke ();
        }

        public async Task InvokePongAsync () {
            OnPong?.Invoke ("pong received");
        }

        public async Task InvokeConnectError () {
            _tokenSource.Cancel ();
            OnConnectError?.Invoke ("");
        }

        public void InvokeConnectTimeout () {
            OnConnectTimeout?.Invoke ("");
        }

        public void InvokeReconnect () {
            OnReconnect?.Invoke ("");
        }

        public void InvokeReconnectAttempt () {
            OnReconnectAttempt?.Invoke ("");
        }

        public void InvokeReconnecting () {
            OnReconnecting?.Invoke ("");
        }

        public void InvokeReconnectError () {
            OnReconnectError?.Invoke ("");
        }

        public void InvokeReconnectFailed () {
            OnReconnectFailed?.Invoke ("");
        }

        public Task InvokeUnhandledEvent (string eventName, ResponseArgs args) {
            UnhandledEvent?.Invoke (eventName, args);
            return Task.CompletedTask;
        }

        public Task InvokeReceivedEvent (string eventName, ResponseArgs args) {
            OnReceivedEvent?.Invoke (eventName, args);
            return Task.CompletedTask;
        }

        public Task InvokeErrorEvent (ResponseArgs args) {
            OnError?.Invoke (args);
            return Task.CompletedTask;
        }

        public void On (string eventName, EventHandler handler) {
            if (EventHandlers.ContainsKey (eventName)) {
                Console.WriteLine (string.Format ("{0} is already on !", eventName));
                return;
            }
            EventHandlers.Add (eventName, handler);
        }

        public void Off (string eventName) {
            if (EventHandlers.ContainsKey (eventName))
                EventHandlers.Remove (eventName);
        }

        private async Task EmitAsync (string eventName, int packetId, object obj) {
            try {
                string text = JsonConvert.SerializeObject (obj);
                var builder = new StringBuilder ();
                builder
                    .Append ("42")
                    .Append (_namespace)
                    .Append (packetId)
                    .Append ('[')
                    .Append ('"')
                    .Append (eventName)
                    .Append ('"')
                    .Append (',')
                    .Append (text)
                    .Append (']');

                string message = builder.ToString ();
                if (State == SocketIOState.Connected) {
                    //Console.WriteLine("SendMessageAsync" + message);
                    await SendMessageAsync (message);
                } else {
                    throw new InvalidOperationException ("Socket connection not ready, emit failure.");
                }
            } catch (Exception e) {
                Console.WriteLine (e.ToString ());
            }

        }

        public async Task EmitAsync (string eventName, object obj) {
            _packetId++;
            await EmitAsync (eventName, _packetId, obj);
        }

        public async Task EmitAsync (string eventName, object obj, EventHandler callback) {
            _packetId++;
            Callbacks.Add (_packetId, callback);
            await EmitAsync (eventName, _packetId, obj);
        }

        public string GetDebugStatus () {
            if (_socket == null) {
                return "null";
            }
            return _socket.State.ToString ();
        }
    }
}