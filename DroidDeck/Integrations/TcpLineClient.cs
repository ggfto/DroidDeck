using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// Conexao TCP de protocolos em linhas de texto (vMix, Companion): le linhas com limite de
    /// tamanho, le blocos binarios depois de uma linha e serializa as escritas.
    /// </summary>
    public sealed class TcpLineClient : IDisposable
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly byte[] _buffer = new byte[64 * 1024];
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private int _start;
        private int _end;

        private TcpLineClient(TcpClient tcp)
        {
            _tcp = tcp;
            _tcp.NoDelay = true;
            _stream = tcp.GetStream();
        }

        public static async Task<TcpLineClient> ConnectAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
        {
            var tcp = new TcpClient();
            try
            {
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connect.CancelAfter(timeout);
                await tcp.ConnectAsync(host, port, connect.Token);
                return new TcpLineClient(tcp);
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        /// <summary>Proxima linha, sem o fim de linha. Null quando o outro lado fechou.</summary>
        public async Task<string?> ReadLineAsync(int maxBytes, CancellationToken ct)
        {
            using var line = new MemoryStream();
            while (true)
            {
                if (_start == _end && !await FillAsync(ct))
                    return line.Length > 0 ? Decode(line) : null;

                var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
                var stop = newline >= 0 ? newline : _end;
                line.Write(_buffer, _start, stop - _start);
                if (line.Length > maxBytes) throw new InvalidDataException("Linha longa demais.");
                _start = newline >= 0 ? stop + 1 : stop;
                if (newline >= 0) return Decode(line);
            }
        }

        /// <summary>Exatamente <paramref name="count"/> bytes (o corpo de uma resposta binaria).</summary>
        public async Task<byte[]> ReadBytesAsync(int count, CancellationToken ct)
        {
            var result = new byte[count];
            var done = 0;
            while (done < count)
            {
                if (_start == _end && !await FillAsync(ct))
                    throw new EndOfStreamException();
                var take = Math.Min(count - done, _end - _start);
                Array.Copy(_buffer, _start, result, done, take);
                _start += take;
                done += take;
            }
            return result;
        }

        public async Task WriteAsync(string text, CancellationToken ct = default)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _writeLock.WaitAsync(ct);
            try { await _stream.WriteAsync(bytes, ct); }
            finally { _writeLock.Release(); }
        }

        private async Task<bool> FillAsync(CancellationToken ct)
        {
            _start = 0;
            _end = await _stream.ReadAsync(_buffer, ct);
            return _end > 0;
        }

        private static string Decode(MemoryStream line)
        {
            var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length);
            return text.EndsWith('\r') ? text[..^1] : text;
        }

        public void Dispose()
        {
            _tcp.Dispose();
            _writeLock.Dispose();
        }

        /// <summary>"estudio.local" ou "192.168.0.10:8099" para host e porta.</summary>
        public static (string host, int port) ParseAddress(string? value, string defaultHost, int defaultPort)
        {
            var s = (value ?? "").Trim();
            if (s.Length == 0) return (defaultHost, defaultPort);
            if (!Uri.TryCreate("tcp://" + s, UriKind.Absolute, out var uri) || uri.Host.Length == 0 ||
                uri.AbsolutePath != "/" || uri.Query.Length > 0)
                throw new InvalidOperationException("Endereço inválido. Use um IP ou nome, com a porta se ela for outra.");
            return (uri.Host, uri.IsDefaultPort || uri.Port <= 0 ? defaultPort : uri.Port);
        }
    }
}
