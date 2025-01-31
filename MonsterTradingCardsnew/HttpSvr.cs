using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonsterTradingCardsnew
{
    public sealed class HttpSvr
    {
        private TcpListener? _Listener;
        public event HttpSvrEventHandler? Incoming;

        private readonly ConcurrentQueue<TcpClient> _WaitingClients = new();
        private readonly ConcurrentQueue<string> _DataTemp = new();
        private readonly ManualResetEvent _BattleReady = new(false);

        public bool Active { get; private set; } = false;

        public void Run()
        {
            if (Active) return;
            Active = true;
            _Listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 10001);
            _Listener.Start();

            while (Active)
            {
                TcpClient client = _Listener.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            byte[] buf = new byte[256];
            string data = string.Empty;

            while (client.GetStream().DataAvailable || string.IsNullOrWhiteSpace(data))
            {
                int bytesRead = await client.GetStream().ReadAsync(buf, 0, buf.Length);
                data += Encoding.UTF8.GetString(buf, 0, bytesRead);
            }

            if (data.Contains("battle"))
            {
                _WaitingClients.Enqueue(client);
                _DataTemp.Enqueue(data);

                if (_WaitingClients.Count == 2)
                {
                    if (_WaitingClients.TryDequeue(out var client1) &&
                        _WaitingClients.TryDequeue(out var client2) &&
                        _DataTemp.TryDequeue(out var dataTempClient1) &&
                        _DataTemp.TryDequeue(out var dataTempClient2))
                    {
                        HttpSvrEventArgs e1 = new HttpSvrEventArgs(client1, dataTempClient1);
                        HttpSvrEventArgs e2 = new HttpSvrEventArgs(client2, dataTempClient2);

                        Task.Run(() => StartBattleThread(e1, e2));
                    }
                }
                else
                {
                    _BattleReady.Reset();
                    _BattleReady.WaitOne();
                }
            }
            else
            {
                Incoming?.Invoke(this, new(client, data));
            }
        }

        private void StartBattleThread(HttpSvrEventArgs player1, HttpSvrEventArgs player2)
        {
            _BattleReady.Set();
            BattleHandler._StartBattle(player1, player2);
        }
    }
}