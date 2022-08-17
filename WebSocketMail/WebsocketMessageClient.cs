using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketMail
{
	class WebsocketMessageClient : IDisposable
	{
		const int MessageReadDelayMs = 100;
		CancellationTokenSource cts;
		Task wsLoopTask;
		readonly int bufferSize;
		bool isDisposed;

		public event EventHandler<string> OnMessage;

		public WebsocketMessageClient (int bufferSize = 1024)
		{
			if (bufferSize < 1) {
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			}
			this.bufferSize = bufferSize;
		}

		public void ConnectAndListen (Uri uri, TimeSpan? reconnectTimeout = null)
		{
			DisposeSocket();
			cts = new CancellationTokenSource();
			wsLoopTask = Task.Run(async () => await StartWebsocketLoop(uri, cts.Token, reconnectTimeout).ConfigureAwait(false));
		}

		async Task StartWebsocketLoop (Uri uri, CancellationToken cancelToken, TimeSpan? reconnectTimeout = null)
		{
			while (!isDisposed && !cancelToken.IsCancellationRequested)
			{
				using (var ws = new ClientWebSocket())
				{
					try
					{
						await ws.ConnectAsync(uri, cancelToken).ConfigureAwait(false);
					}
					catch { }

					while (ws.State == WebSocketState.Open)
					{
						try
						{
							await ReceiveFromWebsocket(ws, cancelToken).ConfigureAwait(false);
						}
						catch
						{
							break;
						}

						await Task.Delay(MessageReadDelayMs);
					}
				}

				if (reconnectTimeout.HasValue)
				{
					await Task.Delay(reconnectTimeout.Value);
				} else {
					break;
				}
			}
		}

		async Task ReceiveFromWebsocket(ClientWebSocket ws, CancellationToken cancelToken = default)
		{
			try
			{
				var buffer = new byte[bufferSize];
				var received = await ws.ReceiveAsync(buffer, cancelToken).ConfigureAwait(false);

				switch (received.MessageType)
				{
					case WebSocketMessageType.Close:
						await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancelToken).ConfigureAwait(false);
						return;
					case WebSocketMessageType.Text:
						var text = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
						HandleWebsocketMessage(text);
						break;
				}
			}
			catch
			{
			}
		}

		void HandleWebsocketMessage(string message)
		{
			OnMessage?.Invoke(this, message);
		}

		void DisposeSocket ()
		{
			cts?.Cancel();
			cts = null;

			wsLoopTask?.Dispose();
			wsLoopTask = null;
		}

		public void Dispose ()
		{
			Dispose(true);
		}

		void Dispose (bool disposing)
		{
			if (isDisposed) {
				return;
			}
			if (disposing) {
				DisposeSocket();
			}
			isDisposed = true;
		}
	}
}
