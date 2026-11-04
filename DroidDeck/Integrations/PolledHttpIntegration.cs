using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DroidDeck.Hubs;
using DroidDeck.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Integrations
{
    /// <summary>
    /// Base das integracoes que so tem HTTP para ler o estado (mAirList, RadioDJ): consulta de
    /// tempos em tempos, publica quando o estado muda e guarda a mensagem de erro para a tela.
    /// </summary>
    public abstract class PolledHttpIntegration : AccountIntegration
    {
        protected static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);

        protected readonly IHttpClientFactory Http;
        private CancellationTokenSource? _wake;
        private string? _lastState;
        private volatile bool _polled;

        protected PolledHttpIntegration(ILogger logger, IHubContext<DeckHub> hub, StreamDeckConfigService config, IHttpClientFactory http)
            : base(logger, hub, config)
        {
            Http = http;
        }

        protected bool Connected { get; private set; }

        protected string? Error { get; private set; }

        protected virtual TimeSpan Interval => TimeSpan.FromSeconds(2);

        /// <summary>Ja tem endereco para consultar.</summary>
        protected abstract bool HasConfig { get; }

        /// <summary>Mensagem quando o programa nao responde (fechado, porta errada, plugin desligado).</summary>
        protected abstract string UnreachableMessage { get; }

        /// <summary>Le o estado. Lancar quer dizer "desconectado"; a mensagem vai para a tela.</summary>
        protected abstract Task PollAsync(CancellationToken ct);

        /// <summary>Esquece o estado lido (ficou desconectado).</summary>
        protected abstract void ResetState();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                using var wake = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                _wake = wake;
                if (!HasConfig)
                {
                    await PublishIfChanged();
                    await Wait(TimeSpan.FromDays(1), wake.Token);
                    continue;
                }
                await PollOnceAsync(stoppingToken);
                await Wait(Interval, wake.Token);
            }
        }

        private async Task PollOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                await PollAsync(stoppingToken);
                Connected = true;
                Error = null;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Connected = false;
                Error = ex is HttpRequestException or TaskCanceledException ? UnreachableMessage : ex.Message;
                ResetState();
            }
            _polled = true;
            await PublishIfChanged();
        }

        /// <summary>Consulta ja, sem esperar o proximo ciclo (uma acao acabou de mudar o estado).</summary>
        protected void PollNow()
        {
            try { _wake?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        /// <summary>Espera a primeira consulta depois de mudar a configuracao, para a tela ja mostrar o resultado.</summary>
        protected async Task<IntegrationStatus> RestartAndWaitAsync()
        {
            _polled = false;
            PollNow();
            for (var i = 0; i < 40 && !_polled; i++) await Task.Delay(150);
            return GetStatus();
        }

        private async Task PublishIfChanged()
        {
            var state = JsonSerializer.Serialize(GetState());
            if (state == _lastState) return;
            _lastState = state;
            await BroadcastAsync();
        }

        /// <summary>"127.0.0.1:9300" ou "http://..." para uma URL http(s) sem barra no fim.</summary>
        public static string NormalizeUrl(string? value, string fallback)
        {
            var s = (value ?? "").Trim();
            if (s.Length == 0) s = fallback;
            if (!s.Contains("://")) s = "http://" + s;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new InvalidOperationException("Endereço inválido. Use um IP ou nome, com a porta se ela for outra.");
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        protected HttpClient NewClient()
        {
            var client = Http.CreateClient();
            client.Timeout = RequestTimeout;
            return client;
        }
    }
}
