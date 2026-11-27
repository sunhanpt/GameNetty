using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ET
{
    public sealed class LocalHttpHost : IDisposable
    {
        private readonly IWebHost host;
        private readonly CancellationTokenSource stopping = new();

        public static bool TryGetPort(string prefix, out int port)
        {
            port = 0;
            if (!Uri.TryCreate(prefix, UriKind.Absolute, out Uri address) || address.Scheme != Uri.UriSchemeHttp ||
                address.Host != IPAddress.Loopback.ToString() || address.AbsolutePath != "/" ||
                address.Query.Length != 0 || address.UserInfo.Length != 0 || address.Fragment.Length != 0)
            {
                return false;
            }

            port = address.Port;
            return port > 0;
        }

        public LocalHttpHost(int port, bool webSockets, Func<HttpContext, CancellationToken, Task> request)
        {
            CancellationToken stoppingToken = this.stopping.Token;
            this.host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.AddServerHeader = false;
                    options.Listen(IPAddress.Loopback, port);
                })
                .ConfigureLogging(logging => logging.ClearProviders())
                .Configure(app =>
                {
                    if (webSockets)
                    {
                        app.UseWebSockets();
                    }

                    app.Run(context => request(context, stoppingToken));
                })
                .Build();

            try
            {
                // Host startup must not wait for a continuation on the game fiber that is creating it.
                Task.Run(() => this.host.StartAsync()).GetAwaiter().GetResult();
            }
            catch
            {
                this.host.Dispose();
                this.stopping.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            this.stopping.Cancel();
            try
            {
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(1));
                Task.Run(() => this.host.StopAsync(timeout.Token)).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.host.Dispose();
                this.stopping.Dispose();
            }
        }
    }
}
