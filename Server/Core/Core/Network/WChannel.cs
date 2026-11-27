using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ET
{
    public class WChannel: AChannel
    {
        public HttpListenerWebSocketContext WebSocketContext { get; }

        private readonly WService Service;

        private readonly WebSocket webSocket;
        private readonly Action onClosed;

        private readonly Queue<MemoryBuffer> queue = new();

        private bool isSending;
        private bool isClosing;
        private Task activeSend = Task.CompletedTask;

        private bool isConnected;
        
        private CancellationTokenSource cancellationTokenSource = new();
        
        public WChannel(long id, HttpListenerWebSocketContext webSocketContext, WService service)
            : this(id, webSocketContext.WebSocket, service)
        {
            this.WebSocketContext = webSocketContext;
        }

        public WChannel(long id, WebSocket webSocket, WService service, Action onClosed = null)
        {
            this.Service = service;
            this.Id = id;
            this.ChannelType = ChannelType.Accept;
            this.webSocket = webSocket;
            this.onClosed = onClosed;

            isConnected = true;
            
            this.Service.ThreadSynchronizationContext.Post(()=>
            {
                this.StartRecv().Coroutine();
                this.StartSend().Coroutine();
            });
        }

        public WChannel(long id, WebSocket webSocket, IPEndPoint ipEndPoint, WService service)
        {
            this.Service = service;
            this.Id = id;
            this.ChannelType = ChannelType.Connect;
            this.webSocket = webSocket;

            isConnected = false;
            
            this.Service.ThreadSynchronizationContext.Post(()=>this.ConnectAsync($"ws://{ipEndPoint}").Coroutine());
        }

        public override void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            this.Id = 0;
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.cancellationTokenSource = null;

            try
            {
                this.webSocket.Dispose();
                while (this.queue.TryDequeue(out MemoryBuffer buffer))
                {
                    this.Service.Recycle(buffer);
                }
            }
            finally
            {
                this.onClosed?.Invoke();
            }
        }

        private async ETTask ConnectAsync(string url)
        {
            try
            {
                await ((ClientWebSocket) this.webSocket).ConnectAsync(new Uri(url), cancellationTokenSource.Token);
                isConnected = true;
                
                this.StartRecv().Coroutine();
                this.StartSend().Coroutine();
            }
            catch (Exception e)
            {
                Log.Error(e);
                this.OnError(ErrorCore.ERR_WebsocketConnectError);
            }
        }

        public void Send(MemoryBuffer memoryBuffer)
        {
            if (this.IsDisposed || this.isClosing)
            {
                this.Service.Recycle(memoryBuffer);
                return;
            }
            this.queue.Enqueue(memoryBuffer);

            if (this.isConnected)
            {
                this.StartSend().Coroutine();
            }
        }

        private async ETTask StartSend()
        {
            if (this.IsDisposed || this.isClosing || this.isSending)
            {
                return;
            }

            this.isSending = true;
            try
            {
                while (!this.IsDisposed && !this.isClosing && this.queue.TryDequeue(out MemoryBuffer stream))
                {
                    try
                    {
                        this.activeSend = this.webSocket.SendAsync(stream.GetMemory(), WebSocketMessageType.Binary, true,
                            this.cancellationTokenSource.Token).AsTask();
                        await this.activeSend;
                    }
                    finally
                    {
                        this.Service.Recycle(stream);
                    }
                }
            }
            catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                this.OnError(ErrorCore.ERR_WebsocketPeerReset);
            }
            catch (Exception e)
            {
                if (!this.IsDisposed && !this.isClosing)
                {
                    Log.Error(e);
                    this.OnError(ErrorCore.ERR_WebsocketSendError);
                }
            }
            finally
            {
                this.isSending = false;
            }
        }

        private readonly byte[] cache = new byte[ushort.MaxValue];

        public async ETTask StartRecv()
        {
            if (this.IsDisposed)
            {
                return;
            }

            try
            {
                while (true)
                {
                    ValueWebSocketReceiveResult receiveResult;
                    int receiveCount = 0;
                    do
                    {
                        receiveResult = await this.webSocket.ReceiveAsync(
                            new Memory<byte>(cache, receiveCount, this.cache.Length - receiveCount),
                            cancellationTokenSource.Token);
                        if (this.IsDisposed)
                        {
                            return;
                        }

                        receiveCount += receiveResult.Count;
                    }
                    while (!receiveResult.EndOfMessage);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await this.ReplyToClose();
                        return;
                    }

                    if (receiveResult.Count > ushort.MaxValue)
                    {
                        await this.webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, $"message too big: {receiveCount}",
                            cancellationTokenSource.Token);
                        this.OnError(ErrorCore.ERR_WebsocketMessageTooBig);
                        return;
                    }

                    MemoryBuffer memoryBuffer = this.Service.Fetch(receiveCount);
                    memoryBuffer.SetLength(receiveCount);
                    memoryBuffer.Seek(0, SeekOrigin.Begin);
                    Array.Copy(this.cache, 0, memoryBuffer.GetBuffer(), 0, receiveCount);
                    this.OnRead(memoryBuffer);
                }
            }
            catch (WebSocketException e) when (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                this.OnError(ErrorCore.ERR_WebsocketPeerReset);
            }
            catch (Exception e)
            {
                if (!this.IsDisposed)
                {
                    Log.Error(e);
                    this.OnError(ErrorCore.ERR_WebsocketRecvError);
                }
            }
        }

        private async ETTask ReplyToClose()
        {
            this.isClosing = true;
            using CancellationTokenSource timeout = new(400);
            try
            {
                await this.activeSend.WaitAsync(timeout.Token);
                if (!this.IsDisposed && this.webSocket.State == WebSocketState.CloseReceived)
                {
                    await this.webSocket.CloseOutputAsync(this.webSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        this.webSocket.CloseStatusDescription, timeout.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // The peer may exit before receiving our reply.
            }
            catch (Exception e)
            {
                if (!this.IsDisposed && e is not WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely })
                {
                    Log.Warning($"WChannel close reply failed: {this.RemoteAddress} {e.Message}");
                }
            }
            finally
            {
                this.OnError(ErrorCore.ERR_WebsocketPeerReset);
            }
        }
        
        private void OnRead(MemoryBuffer memoryStream)
        {
            try
            {
                this.Service.ReadCallback(this.Id, memoryStream);
            }
            catch (Exception e)
            {
                Log.Error(e);
                this.OnError(ErrorCore.ERR_PacketParserError);
            }
        }
        
        private void OnError(int error)
        {
            if (this.IsDisposed)
            {
                return;
            }
            string status = error == ErrorCore.ERR_WebsocketPeerReset ? "closed" : "error";
            Log.Info($"WChannel {status}: {error} {this.RemoteAddress}");
			
            long channelId = this.Id;
			
            this.Service.Remove(channelId, error);
			
            this.Service.ErrorCallback?.Invoke(channelId, error);
        }
    }
}
