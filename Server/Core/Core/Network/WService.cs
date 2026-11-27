using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;

namespace ET
{
    public class WService: AService
    {
        private long idGenerater = 200000000;
        
        private HttpListener httpListener;
        private readonly LocalHttpHost localHost;
        
        private readonly Dictionary<long, WChannel> channels = new Dictionary<long, WChannel>();

        public ThreadSynchronizationContext ThreadSynchronizationContext;

        public WService(IEnumerable<string> prefixs) : this()
        {
            string[] prefixes = prefixs.ToArray();
            if (prefixes.Length == 1 && LocalHttpHost.TryGetPort(prefixes[0], out int port))
            {
                this.localHost = new LocalHttpHost(port, true, this.AcceptLocal);
                return;
            }

            this.httpListener = new HttpListener();
            StartAccept(prefixes).Coroutine();
        }
        
        public WService()
        {
            this.ServiceType = ServiceType.Outer;
            this.ThreadSynchronizationContext = new ThreadSynchronizationContext();
        }
        
        private long GetId
        {
            get
            {
                return ++this.idGenerater;
            }
        }
        
        public override void Create(long id, IPEndPoint ipEndpoint)
        {
			ClientWebSocket webSocket = new();
            WChannel channel = new(id, webSocket, ipEndpoint, this);
            this.channels[channel.Id] = channel;
        }

        public override void Update()
        {
            this.ThreadSynchronizationContext.Update();
        }

        public override void Remove(long id, int error = 0)
        {
            WChannel channel;
            if (!this.channels.TryGetValue(id, out channel))
            {
                return;
            }

            channel.Error = error;

            this.channels.Remove(id);
            channel.Dispose();
        }

        public override bool IsDisposed()
        {
            return this.ThreadSynchronizationContext == null;
        }

        protected void Get(long id, IPEndPoint ipEndPoint)
        {
            if (!this.channels.TryGetValue(id, out _))
            {
                this.Create(id, ipEndPoint);
            }
        }
        
        public WChannel Get(long id)
        {
            WChannel channel = null;
            this.channels.TryGetValue(id, out channel);
            return channel;
        }

        public override void Dispose()
        {
            if (this.IsDisposed()) return;
            base.Dispose();

            this.ThreadSynchronizationContext = null;
            foreach (WChannel channel in this.channels.Values)
            {
                channel.Dispose();
            }
            this.channels.Clear();
            this.localHost?.Dispose();
            this.httpListener?.Close();
            this.httpListener = null;
        }

        private async Task AcceptLocal(HttpContext context, CancellationToken stopping)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            ThreadSynchronizationContext dispatcher = this.ThreadSynchronizationContext;
            if (dispatcher == null) return;
            using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(stopping, context.RequestAborted);
            CancellationToken cancelled = abort.Token;
            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            IPEndPoint peer = new(context.Connection.RemoteIpAddress, context.Connection.RemotePort);
            TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.Post(() =>
            {
                if (cancelled.IsCancellationRequested || this.IsDisposed())
                {
                    finished.TrySetResult();
                    return;
                }

                long id = this.GetId;
                WChannel channel = new(id, socket, this, () => finished.TrySetResult()) { RemoteAddress = peer };
                this.channels.Add(id, channel);
                try
                {
                    this.AcceptCallback(id, peer);
                }
                catch
                {
                    this.Remove(id);
                    throw;
                }
            });

            try
            {
                await finished.Task.WaitAsync(cancelled);
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
            {
            }
        }

        private async ETTask StartAccept(IEnumerable<string> prefixs)
        {
            try
            {
                foreach (string prefix in prefixs)
                {
                    this.httpListener.Prefixes.Add(prefix);
                }
                
                httpListener.Start();

                while (!this.IsDisposed())
                {
                    try
                    {
                        HttpListenerContext httpListenerContext = await this.httpListener.GetContextAsync();

                        HttpListenerWebSocketContext webSocketContext = await httpListenerContext.AcceptWebSocketAsync(null);

                        WChannel channel = new WChannel(this.GetId, webSocketContext, this);
                        channel.RemoteAddress = httpListenerContext.Request.RemoteEndPoint;
                        this.channels[channel.Id] = channel;

                        this.AcceptCallback(channel.Id, channel.RemoteAddress);
                    }
                    catch (Exception e) when (!this.IsDisposed())
                    {
                        Log.Error(e);
                    }
                }
            }
            catch (HttpListenerException e) when (OperatingSystem.IsWindows() && e.NativeErrorCode == 5)
            {
                throw new InvalidOperationException($"Windows HTTP.sys 拒绝监听 {string.Join(", ", prefixs)}。请为运行服务端的账号配置对应的 URL ACL。", e);
            }
            catch (Exception) when (this.IsDisposed())
            {
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Send(long channelId, MemoryBuffer memoryBuffer)
        {
            this.channels.TryGetValue(channelId, out WChannel channel);
            if (channel == null)
            {
                return;
            }
            channel.Send(memoryBuffer);
        }
    }
}
