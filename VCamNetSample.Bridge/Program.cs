using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using VCamNetSample.Shared;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Bridge starting...");

        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:8765/");
        listener.Start();

        Console.WriteLine("WebSocket listening on ws://127.0.0.1:8765/");

        while (true)
        {
            var ctx = await listener.GetContextAsync();

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            var wsContext = await ctx.AcceptWebSocketAsync(null);
            Console.WriteLine("WebSocket connected");

            _ = Task.Run(() => HandleClient(wsContext.WebSocket));
        }
    }

    static async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[16 * 1024 * 1024]; // hasta ~4K RGBA sin realloc

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                // Read until EndOfMessage
                int totalBytesReceived = 0;
                WebSocketReceiveResult result;
                do
                {
                    // Check if buffer is full
                    if (totalBytesReceived >= buffer.Length)
                    {
                        Console.WriteLine("Frame too large for buffer, force disconnect.");
                        return; 
                    }

                    result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer, totalBytesReceived, buffer.Length - totalBytesReceived),
                        CancellationToken.None
                    );
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    totalBytesReceived += result.Count;
                    
                } while (!result.EndOfMessage);

                 if (result.MessageType == WebSocketMessageType.Close)
                    break;

                int count = totalBytesReceived;

                if (count < 8)
                {
                     Console.WriteLine($"Ignored small frame: {count} bytes");
                     continue;
                }

                int width  = BitConverter.ToInt32(buffer, 0);
                int height = BitConverter.ToInt32(buffer, 4);

                if (width <= 0 || height <= 0 || width > 10000 || height > 10000)
                {
                    Console.WriteLine($"Invalid dimensions {width}x{height}, ignoring frame.");
                    continue;
                }

                long expectedBytes = (long)width * height * 4;
                
                // Extra check: Check if received data matches expected size (strict) or is at least enough
                if (count < 8 + expectedBytes)
                {
                    Console.WriteLine($"Frame incomplete. Had {count}, expected {8 + expectedBytes}");
                    continue; 
                }

                // Copia exacta de RGBA
                var rgba = new byte[expectedBytes];
                Buffer.BlockCopy(buffer, 8, rgba, 0, (int)expectedBytes);

                // 👉 Punto clave: alimentar la cámara virtual
                Globals.Frames.SetLatest(rgba, width, height);
                // Console.WriteLine($"Processed frame {width}x{height}, {count} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("WS error: " + ex.Message);
        }
        finally
        {
            try { ws.Dispose(); } catch {}
            Console.WriteLine("WebSocket disconnected");
        }
    }
}
