using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Threading.Tasks;

namespace WebSocketCompliance
{
    public sealed class WebSocketServerMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketServerMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public Task InvokeAsync(HttpContext context)
        {
            // Detect if an opaque upgrade is available. If so, add a websocket upgrade.
            var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();

            if (upgradeFeature != null)
            {
                var request = new WebSocketRequest(context, upgradeFeature);

                if (request.IsWebSocketRequest)
                {
                    context.Features.Set<IHttpWebSocketFeature>(request);
                }
            }

            return _next(context);
        }
    }
}
