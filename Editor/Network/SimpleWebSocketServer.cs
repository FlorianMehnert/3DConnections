using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class SimpleWebSocketServer
{
    private readonly HttpListener listener;
    private readonly List<WebSocket> clients = new();
    private bool running;

    public SimpleWebSocketServer(int port)
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        running = true;
        listener.Start();
        _ = AcceptLoop();
    }

    public void Stop()
    {
        running = false;
        listener.Stop();
    }

    private async Task AcceptLoop()
    {
        while (running)
        {
            var ctx = await listener.GetContextAsync();

            if (ctx.Request.IsWebSocketRequest)
            {
                var wsContext = await ctx.AcceptWebSocketAsync(null);
                var socket = wsContext.WebSocket;
                lock (clients)
                {
                    clients.Add(socket);
                }

                _ = ReceiveLoop(socket);
            }
            else
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
            }
        }
    }

    private async Task ReceiveLoop(WebSocket socket)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                lock (clients)
                {
                    clients.Remove(socket);
                }
            }
        }
    }

    public void Broadcast(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        lock (clients)
        {
            foreach (var socket in clients.ToArray())
                if (socket.State == WebSocketState.Open)
                    socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}