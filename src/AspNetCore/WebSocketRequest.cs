using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketCompliance
{
    internal sealed class WebSocketRequest : IHttpWebSocketFeature
    {
        private static Type s_managedWebSocketType;
        private static ConstructorInfo s_webSocketCtor;
        private static Type s_webSocketOptionsType;

        private readonly IHttpUpgradeFeature _upgradeFeature;

        public WebSocketRequest(HttpContext context, IHttpUpgradeFeature upgradeFeature)
        {
            Context = context;
            IsWebSocketRequest = upgradeFeature.IsUpgradableRequest && CheckSupportedWebSocketRequest();

            _upgradeFeature = upgradeFeature;
        }

        public HttpContext Context { get; }

        public bool IsWebSocketRequest { get; }

        public async Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            Debug.Assert(IsWebSocketRequest);

            var response = Context.Response;

            var deflateOptions = ParseDeflateOptions(Context.Request.Headers[WebSocketHeaders.SecWebSocketExtensions].FirstOrDefault() ?? string.Empty, out var responseExtensions);

            if (deflateOptions is not null)
            {
                response.Headers[WebSocketHeaders.SecWebSocketExtensions] = responseExtensions;
            }
            response.Headers[WebSocketHeaders.Connection] = "Upgrade";
            response.Headers[WebSocketHeaders.ConnectionUpgrade] = "websocket";
            response.Headers[WebSocketHeaders.SecWebSocketAccept] = CreateResponseKey();

            // Sets status code to 101
            var stream = await _upgradeFeature.UpgradeAsync();

            if (stream == null)
            {
                Context.Abort();

                throw new WebSocketException("Failed to upgrade websocket connection.");
            }

            s_managedWebSocketType ??= typeof(WebSocket).Assembly.GetType("System.Net.WebSockets.ManagedWebSocket", throwOnError: true);
            s_webSocketCtor ??= s_managedWebSocketType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).First(x => x.GetParameters().Length == 2);
            s_webSocketOptionsType ??= typeof(WebSocket).Assembly.GetType("System.Net.WebSockets.WebSocketCreationOptions", throwOnError: true);

            object options = Activator.CreateInstance(s_webSocketOptionsType);

            ((dynamic)options).IsServer = true;
            ((dynamic)options).KeepAliveInterval = TimeSpan.Zero;
            options.GetType().GetProperty("DangerousDeflateOptions").SetValue(options, deflateOptions);

            return (WebSocket)s_webSocketCtor.Invoke(new object[] { stream, options });
        }

        private static object ParseDeflateOptions(ReadOnlySpan<char> extension, out string response)
        {
            response = string.Empty;

            if (extension.IndexOf("permessage-deflate") < 0)
            {
                return null;
            }

            var result = Activator.CreateInstance(typeof(WebSocket).Assembly.GetType("System.Net.WebSockets.WebSocketDeflateOptions"));
            dynamic options = result;

            response = "permessage-deflate";

            while (true)
            {
                int end = extension.IndexOf(';');
                ReadOnlySpan<char> value = (end >= 0 ? extension[..end] : extension).Trim();

                if (value.Length > 0)
                {
                    if (value.SequenceEqual("client_no_context_takeover"))
                    {
                        options.ClientContextTakeover = false;
                        response += "; client_no_context_takeover";

                    }
                    else if (value.SequenceEqual("server_no_context_takeover"))
                    {
                        options.ServerContextTakeover = false;
                        response += "; server_no_context_takeover";
                    }
                    else if (value.StartsWith("client_max_window_bits"))
                    {
                        options.ClientMaxWindowBits = value.StartsWith("client_max_window_bits=") ?
                            int.Parse(value["client_max_window_bits=".Length..]) : 15;

                        response += "; client_max_window_bits=" + options.ClientMaxWindowBits.ToString();
                    }
                    else if (value.StartsWith("server_max_window_bits"))
                    {
                        options.ServerMaxWindowBits = value.StartsWith("server_max_window_bits=") ?
                            int.Parse(value["server_max_window_bits=".Length..]) : 15;

                        response += "; server_max_window_bits=" + options.ServerMaxWindowBits.ToString();
                    }
                }

                if (end < 0)
                {
                    break;
                }
                extension = extension[(end + 1)..];
            }

            return result;
        }

        private bool CheckSupportedWebSocketRequest()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;

            var request = Context.Request;
            var version = request.Headers[WebSocketHeaders.SecWebSocketVersion];
            var key = request.Headers[WebSocketHeaders.SecWebSocketKey];

            if (!comparer.Equals(request.Method, "GET"))
            {
                return false;
            }
            else if (!comparer.Equals(request.Headers[WebSocketHeaders.ConnectionUpgrade], "websocket"))
            {
                return false;
            }
            else if (!WebSocketHeaders.SupportedVersion.Equals(version) || !IsRequestKeyValid(key))
            {
                return false;
            }

            return true;
        }

        private static bool IsRequestKeyValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                return Convert.FromBase64String(value).Length == 16;
            }
            catch
            {
                return false;
            }
        }


        private string CreateResponseKey()
        {
            string key = Context.Request.Headers[WebSocketHeaders.SecWebSocketKey];

            // "The value of this header field is constructed by concatenating /key/, defined above in step 4
            // in Section 4.2.2, with the String "258EAFA5-E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of
            // this concatenated value to obtain a 20-Byte value and base64-encoding"
            // https://tools.ietf.org/html/rfc6455#section-4.2.2
            var mergedBytes = Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
            var hashedBytes = SHA1.HashData(mergedBytes);

            return Convert.ToBase64String(hashedBytes);
        }
    }
}
