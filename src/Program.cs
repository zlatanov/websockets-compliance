using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketCompliance
{
    class Program
    {
        private const string AgentName = "ClientWebSocket";
        private static readonly Uri BaseUri = new("ws://127.0.0.1:9001");

        static async Task Main(string[] args)
        {
            object deflateOptions = Activator.CreateInstance(typeof(WebSocket).Assembly.GetType("System.Net.WebSockets.WebSocketDeflateOptions"));
            PropertyInfo deflateOptionsProperty = typeof(ClientWebSocketOptions).GetProperty("DangerousDeflateOptions");
            int caseCount = 0;

            if (args.Length == 0)
            {
                Console.WriteLine("Fetching case count...");
                caseCount = await GetCaseCountAsync();
            }

            Memory<byte> buffer = new byte[1024 * 1024];

            foreach (int caseId in args.Length > 0 ? ParseCases(args) : Enumerable.Range(1, caseCount))
            {
                Console.Write($"Running test case {caseId}...");

                using var client = new ClientWebSocket
                {
                    Options =
                    {
                        KeepAliveInterval = TimeSpan.Zero
                    }
                };
                deflateOptionsProperty.SetValue(client.Options, deflateOptions);

                try
                {
                    await client.ConnectAsync(new Uri(BaseUri, $"runCase?case={caseId}&agent={AgentName}"), CancellationToken.None);

                    while (true)
                    {

                        ValueWebSocketReceiveResult result = await client.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }
                        await client.SendAsync(buffer.Slice(0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    }
                }
                catch (WebSocketException)
                {
                }

                if (client.State is not (WebSocketState.Aborted or WebSocketState.Closed))
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, client.CloseStatusDescription, CancellationToken.None);
                }
                Console.WriteLine(" Completed.");
            }

            Console.WriteLine("Updating reports...");
            await UpdateReportsAsync();
            Console.WriteLine("Done");
        }

        private static IEnumerable<int> ParseCases(string[] args)
        {
            foreach (var arg in args)
            {
                string[] parts = arg.Split('-', StringSplitOptions.RemoveEmptyEntries);
                int start = int.Parse(parts[0]);

                if (parts.Length == 2)
                {
                    int end = int.Parse(parts[1]);

                    while (start <= end)
                    {
                        yield return start;

                        start += 1;
                    }
                }
                else
                {
                    yield return start;
                }
            }
        }

        private static async Task<int> GetCaseCountAsync()
        {
            using var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri(BaseUri, "getCaseCount"), CancellationToken.None);
            Memory<byte> buffer = new byte[16];
            ValueWebSocketReceiveResult result = await client.ReceiveAsync(buffer, CancellationToken.None);

            return int.Parse(Encoding.UTF8.GetString(buffer.Span.Slice(0, result.Count)));
        }

        private static async Task UpdateReportsAsync()
        {
            using var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri(BaseUri, "updateReports?agent=" + AgentName), CancellationToken.None);
            ValueWebSocketReceiveResult result = await client.ReceiveAsync(Memory<byte>.Empty, CancellationToken.None);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, client.CloseStatusDescription, CancellationToken.None);
        }
    }
}
