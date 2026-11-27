using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ET.Server
{
    [EntitySystemOf(typeof(HttpComponent))]
    [FriendOf(typeof(HttpComponent))]
    public static partial class HttpComponentSystem
    {
        [EntitySystem]
        private static void Awake(this HttpComponent self, string address)
        {
            if (self.IScene.SceneType == SceneType.RouterManager && LocalHttpHost.TryGetPort(address, out int port))
            {
                ThreadSynchronizationContext dispatcher = self.Fiber().ThreadSynchronizationContext;
                self.LocalHost = new LocalHttpHost(port, false, (context, stopping) => ServeLocalRouter(context, dispatcher, stopping));
                return;
            }

            try
            {
                self.Listener = new HttpListener();

                foreach (string s in address.Split(';'))
                {
                    if (s.Trim() == "")
                    {
                        continue;
                    }
                    self.Listener.Prefixes.Add(s);
                }

                self.Listener.Start();

                self.Accept().Coroutine();
            }
            catch (HttpListenerException e) when (OperatingSystem.IsWindows() && e.NativeErrorCode == 5)
            {
                throw new InvalidOperationException($"Windows HTTP.sys 拒绝监听 {address}。请为运行服务端的账号配置对应的 URL ACL。", e);
            }
        }
        
        [EntitySystem]
        private static void Destroy(this HttpComponent self)
        {
            self.LocalHost?.Dispose();
            self.LocalHost = null;
            self.Listener?.Close();
            self.Listener = null;
        }

        private static async Task ServeLocalRouter(HttpContext context, ThreadSynchronizationContext dispatcher, CancellationToken stopping)
        {
            if (context.Request.Path != "/get_router")
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = HttpMethods.Get;
                return;
            }

            using CancellationTokenSource abort = CancellationTokenSource.CreateLinkedTokenSource(stopping, context.RequestAborted);
            CancellationToken cancelled = abort.Token;
            TaskCompletionSource<byte[]> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            dispatcher.Post(() =>
            {
                if (cancelled.IsCancellationRequested) return;
                try
                {
                    ready.TrySetResult(MongoHelper.ToJson(HttpGetRouterHandler.CreateResponse()).ToUtf8());
                }
                catch (Exception error)
                {
                    Log.Error(error);
                    ready.TrySetException(error);
                }
            });

            try
            {
                byte[] response = await ready.Task.WaitAsync(cancelled);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength = response.Length;
                await context.Response.Body.WriteAsync(response, cancelled);
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
            {
            }
        }

        private static async ETTask Accept(this HttpComponent self)
        {
            long instanceId = self.InstanceId;
            while (self.InstanceId == instanceId)
            {
                try
                {
                    HttpListenerContext context = await self.Listener.GetContextAsync();
                    self.Handle(context).Coroutine();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        private static async ETTask Handle(this HttpComponent self, HttpListenerContext context)
        {
            try
            {
                IHttpHandler handler = HttpDispatcher.Instance.Get(self.IScene.SceneType, context.Request.Url.AbsolutePath);
                if (handler != null)
                {
                    await handler.Handle(self.Scene(), context);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            context.Request.InputStream.Dispose();
            context.Response.OutputStream.Dispose();
        }
    }
}
