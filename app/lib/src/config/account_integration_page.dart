import 'dart:async';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/studio/companion_surface_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Um campo da tela de uma integração com conta.
class IntegrationField {
  final String key;
  final String label;
  final String? hint;
  final bool secret;

  const IntegrationField(this.key, this.label, {this.hint, this.secret = false});
}

/// Descrição de uma integração com conta (Home Assistant, Spotify, Twitch, YouTube).
/// Os textos ficam em português e são traduzidos com [tr] na tela.
/// O fluxo é o mesmo para todas, no PC: GET/POST/DELETE /api/integrations/{name}.
class AccountIntegrationSpec {
  final String name;
  final String title;
  final IconData icon;
  final String stateEvent;
  final List<IntegrationField> fields;

  /// Passo a passo para criar o app/token no serviço.
  final List<String> steps;

  /// Texto do botão principal (ex.: "Entrar com o Spotify").
  final String connectLabel;

  /// O login abre no navegador do PC (OAuth).
  final bool browserLogin;

  /// Resumo do estado ao vivo, mostrado quando conectado.
  final String? Function(Map<String, dynamic> state)? summary;

  /// Tela que a integração abre depois de conectada, e o texto do botão que leva a ela.
  final WidgetBuilder? connectedPage;
  final String? connectedPageLabel;

  const AccountIntegrationSpec({
    required this.name,
    required this.title,
    required this.icon,
    required this.stateEvent,
    required this.fields,
    required this.steps,
    this.connectLabel = 'Conectar',
    this.browserLogin = false,
    this.summary,
    this.connectedPage,
    this.connectedPageLabel,
  });
}

String? _liveSummary(Map<String, dynamic> s) => s['live'] == true
    ? s['viewers'] != null
        ? tr('ao vivo, {count} assistindo', {'count': s['viewers']})
        : tr('ao vivo')
    : tr('fora do ar');

String? _spotifySummary(Map<String, dynamic> s) => s['track'] != null
    ? '${s['playing'] == true ? tr('tocando') : tr('pausado')}: ${s['track']}'
    : null;

const homeAssistantSpec = AccountIntegrationSpec(
  name: 'homeassistant',
  title: 'Home Assistant',
  icon: Icons.home_outlined,
  stateEvent: 'ReceiveHomeAssistantState',
  fields: [
    IntegrationField('url', 'Endereço', hint: 'http://homeassistant.local:8123'),
    IntegrationField('token', 'Token de acesso de longa duração', secret: true),
  ],
  steps: [
    'No Home Assistant, abra o seu perfil (canto inferior esquerdo) → aba Segurança.',
    'Em "Tokens de acesso de longa duração", crie um token com o nome DroidDeck.',
    'Copie o token (ele só aparece uma vez) e cole aqui, com o endereço do Home Assistant.',
  ],
);

const spotifySpec = AccountIntegrationSpec(
  name: 'spotify',
  title: 'Spotify',
  icon: Icons.music_note,
  stateEvent: 'ReceiveSpotifyState',
  connectLabel: 'Entrar com o Spotify',
  summary: _spotifySummary,
  browserLogin: true,
  fields: [IntegrationField('clientId', 'Client ID')],
  steps: [
    'Entre em developer.spotify.com/dashboard e crie um app (Create app).',
    'Em "Redirect URIs", cadastre exatamente o endereço de retorno mostrado abaixo.',
    'Marque "Web API", salve e copie o Client ID para cá.',
    'Controlar a reprodução exige Spotify Premium.',
  ],
);

const twitchSpec = AccountIntegrationSpec(
  name: 'twitch',
  title: 'Twitch',
  icon: Icons.sensors,
  stateEvent: 'ReceiveTwitchState',
  connectLabel: 'Entrar com a Twitch',
  summary: _liveSummary,
  fields: [IntegrationField('clientId', 'Client ID')],
  steps: [
    'Entre em dev.twitch.tv/console/apps e registre um aplicativo.',
    'Em "URLs de redirecionamento OAuth" use http://localhost (não é usado).',
    'Em "Tipo de cliente", escolha Público. Copie o Client ID para cá.',
    'Ao conectar, a Twitch mostra um código: confirme em twitch.tv/activate.',
  ],
);

const youTubeSpec = AccountIntegrationSpec(
  name: 'youtube',
  title: 'YouTube',
  icon: Icons.smart_display_outlined,
  stateEvent: 'ReceiveYouTubeState',
  connectLabel: 'Entrar com o Google',
  summary: _liveSummary,
  browserLogin: true,
  fields: [
    IntegrationField('clientId', 'Client ID', hint: '….apps.googleusercontent.com'),
    IntegrationField('clientSecret', 'Client Secret', secret: true),
  ],
  steps: [
    'No console.cloud.google.com, crie um projeto e ative a "YouTube Data API v3".',
    'Em "Tela de permissão OAuth", escolha Externo e adicione o seu e-mail como usuário de teste.',
    'Em Credenciais → Criar credenciais → ID do cliente OAuth → "App para computador".',
    'Copie o Client ID e o Client Secret para cá.',
  ],
);

// ---- Integrações locais (sem conta na nuvem), com a mesma tela ----

const hueSpec = AccountIntegrationSpec(
  name: 'hue',
  title: 'Philips Hue',
  icon: Icons.emoji_objects_outlined,
  stateEvent: 'ReceiveHueState',
  connectLabel: 'Parear',
  fields: [
    IntegrationField('bridge', 'IP do Hue Bridge', hint: 'Vazio = procurar na rede'),
  ],
  steps: [
    'O PC e o Hue Bridge precisam estar na mesma rede.',
    'Digite o IP do bridge (no app Hue: Configurações → Bridges) ou deixe vazio para procurar.',
    'Aperte o botão redondo em cima do Hue Bridge.',
    'Em até 30 segundos, toque em Parear aqui.',
  ],
);

const mqttSpec = AccountIntegrationSpec(
  name: 'mqtt',
  title: 'MQTT',
  icon: Icons.hub_outlined,
  stateEvent: 'ReceiveMqttState',
  fields: [
    IntegrationField('server', 'Broker', hint: 'mqtt://192.168.0.10:1883'),
    IntegrationField('username', 'Usuário (opcional)'),
    IntegrationField('password', 'Senha (opcional)', secret: true),
  ],
  steps: [
    'Use o endereço do seu broker (Mosquitto, o add-on do Home Assistant, EMQX...). A porta padrão é 1883.',
    'Para TLS, comece com mqtts:// (porta padrão 8883).',
    'Depois, crie botões do tipo MQTT com o tópico e a mensagem a publicar.',
  ],
);

const streamerBotSpec = AccountIntegrationSpec(
  name: 'streamerbot',
  title: 'Streamer.bot',
  icon: Icons.smart_toy_outlined,
  stateEvent: 'ReceiveStreamerBotState',
  fields: [
    IntegrationField('url', 'Endereço do WebSocket Server', hint: 'ws://127.0.0.1:8080/'),
    IntegrationField('password', 'Senha (se tiver)', secret: true),
  ],
  steps: [
    'No Streamer.bot, abra Servers/Clients → WebSocket Server.',
    'Marque "Auto Start" e clique em Start Server (o padrão é 127.0.0.1, porta 8080).',
    'Streamer.bot em outro PC: lá, troque o endereço para 0.0.0.0; aqui, use o IP daquele PC.',
    'Se ligou "Authentication", preencha a mesma senha.',
  ],
);

const vtubeStudioSpec = AccountIntegrationSpec(
  name: 'vtubestudio',
  title: 'VTube Studio',
  icon: Icons.face,
  stateEvent: 'ReceiveVTubeStudioState',
  fields: [
    IntegrationField('url', 'Endereço da API', hint: 'ws://localhost:8001'),
  ],
  steps: [
    'No VTube Studio, abra as Configurações (engrenagem) e ligue "Iniciar API (permitir plugins)".',
    'Toque em Conectar aqui: o VTube Studio pergunta se permite o DroidDeck. Clique em Permitir.',
    'A porta padrão é 8001; se mudou no VTube Studio, mude aqui também.',
  ],
);

// ---- Estúdio: rádio e TV ----

const vmixSpec = AccountIntegrationSpec(
  name: 'vmix',
  title: 'vMix',
  icon: Icons.live_tv,
  stateEvent: 'ReceiveVmixState',
  fields: [
    IntegrationField('host', 'Endereço do PC com o vMix', hint: '127.0.0.1'),
  ],
  steps: [
    'Abra o vMix. A TCP API dele (porta 8099) já fica ligada, sem configuração nem senha.',
    'vMix neste PC: toque em Conectar. Em outro PC: digite o IP dele e libere a porta 8099 no firewall de lá.',
    'Depois, crie botões do tipo vMix. As entradas aparecem na lista.',
  ],
);

Widget _companionSurface(BuildContext context) => const CompanionSurfacePage();

const companionSpec = AccountIntegrationSpec(
  name: 'companion',
  title: 'Companion',
  icon: Icons.grid_view,
  stateEvent: 'ReceiveCompanionState',
  connectedPage: _companionSurface,
  connectedPageLabel: 'Abrir a superfície',
  fields: [
    IntegrationField('address', 'Endereço do Companion', hint: '127.0.0.1'),
    IntegrationField('columns', 'Colunas', hint: '8'),
    IntegrationField('rows', 'Linhas', hint: '4'),
  ],
  steps: [
    'Abra o Companion. A porta da Satellite API (16622) já fica ligada por padrão.',
    'Companion neste PC: toque em Conectar. Em outro PC: digite o IP dele.',
    'O DroidDeck aparece em Surfaces no Companion, como um aparelho com essa grade.',
    'Abra a superfície aqui ou crie um botão do tipo Companion no deck.',
  ],
);

const mairlistSpec = AccountIntegrationSpec(
  name: 'mairlist',
  title: 'mAirList',
  icon: Icons.radio,
  stateEvent: 'ReceiveMairlistState',
  fields: [
    IntegrationField('url', 'Endereço da interface REST', hint: 'http://127.0.0.1:9300'),
    IntegrationField('username', 'Usuário'),
    IntegrationField('password', 'Senha', secret: true),
  ],
  steps: [
    'No mAirList, abra Configuração → Remote Control e adicione uma interface REST (a porta padrão é 9300).',
    'Crie um usuário e ligue os plugins ExecuteCommand (comandos) e GeneralRead (estado no ar).',
    'Preencha aqui o endereço, o usuário e a senha.',
    'Licença: segundo o fórum do mAirList, a interface REST pede a edição Advanced ou superior.',
  ],
);

const radioDjSpec = AccountIntegrationSpec(
  name: 'radiodj',
  title: 'RadioDJ',
  icon: Icons.radio,
  stateEvent: 'ReceiveRadioDjState',
  summary: _radioDjSummary,
  fields: [
    IntegrationField('url', 'Endereço do plugin REST Server', hint: 'http://127.0.0.1:8080'),
    IntegrationField('password', 'Senha do plugin', secret: true),
  ],
  steps: [
    'No RadioDJ 2, copie o Plugin_RestServer da pasta "Disabled Plugins" para a pasta Plugins e abra o RadioDJ.',
    'Nas opções do plugin, defina o endereço, a porta e a senha, e inicie o servidor.',
    'Preencha aqui o mesmo endereço e a mesma senha.',
  ],
);

String? _radioDjSummary(Map<String, dynamic> s) => s['title'] != null
    ? '${tr('tocando')}: ${s['artist'] != null ? '${s['artist']} - ' : ''}${s['title']}'
    : null;

/// Tela de configuração de uma integração com conta.
class AccountIntegrationPage extends StatefulWidget {
  final AccountIntegrationSpec spec;

  const AccountIntegrationPage({super.key, required this.spec});

  @override
  State<AccountIntegrationPage> createState() => _AccountIntegrationPageState();
}

class _AccountIntegrationPageState extends State<AccountIntegrationPage> {
  final Map<String, TextEditingController> _ctrls = {};
  Map<String, dynamic> _status = {};
  bool _busy = false;
  Timer? _poll;

  AccountIntegrationSpec get spec => widget.spec;
  DroidDeckClient get _client => Injector.get<DroidDeckClient>();
  String get _path => '/api/integrations/${spec.name}';

  @override
  void initState() {
    super.initState();
    for (final f in spec.fields) {
      _ctrls[f.key] = TextEditingController();
    }
    Injector.get<SignalRService>().listenState(spec.stateEvent);
    _load(fillFields: true);
  }

  @override
  void dispose() {
    _poll?.cancel();
    for (final c in _ctrls.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _load({bool fillFields = false}) async {
    try {
      final s = Map<String, dynamic>.from(await _client.getJson(_path) as Map);
      if (!mounted) return;
      setState(() => _status = s);
      if (fillFields) {
        final fields = Map<String, dynamic>.from(s['fields'] as Map? ?? {});
        fields.forEach((k, v) {
          if ('$v'.isNotEmpty) _ctrls[k]?.text = '$v';
        });
      }
      _watchLogin();
    } catch (_) {}
  }

  /// Enquanto o login está no navegador do PC, consulta até ele terminar.
  void _watchLogin() {
    final pending = _status['loginPending'] == true;
    if (pending && _poll == null) {
      _poll = Timer.periodic(const Duration(seconds: 2), (_) => _load());
    } else if (!pending) {
      _poll?.cancel();
      _poll = null;
    }
  }

  void _snack(String m) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
    }
  }

  Future<void> _connect() async {
    setState(() => _busy = true);
    try {
      final body = {
        for (final f in spec.fields)
          if (_ctrls[f.key]!.text.trim().isNotEmpty) f.key: _ctrls[f.key]!.text.trim(),
      };
      final s = Map<String, dynamic>.from(await _client.postJson(_path, body) as Map);
      if (!mounted) return;
      setState(() => _status = s);
      _watchLogin();
      if (s['message'] != null) {
        _snack('${s['message']}');
      } else if (spec.browserLogin && s['loginPending'] == true) {
        _snack(tr('Continue o login no navegador do PC.'));
      }
    } catch (e) {
      _snack(tr('Não foi possível falar com o PC.'));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _logout() async {
    setState(() => _busy = true);
    try {
      final s = Map<String, dynamic>.from(await _client.deleteJson(_path) as Map);
      if (mounted) setState(() => _status = s);
    } catch (_) {
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final muted = TextStyle(
        fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant);
    final connected = _status['connected'] == true;
    final redirect = _status['redirectUri'] as String?;
    return Scaffold(
      appBar: AppBar(title: Text(tr(spec.title))),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _statusCard(),
              if (_status['userCode'] != null) ...[
                const SizedBox(height: 12),
                _deviceCodeCard(),
              ],
              const SizedBox(height: 16),
              Card(
                child: ExpansionTile(
                  initiallyExpanded: _status['configured'] != true,
                  leading: const Icon(Icons.help_outline),
                  title: Text(tr('Como configurar')),
                  childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                  children: [
                    for (var i = 0; i < spec.steps.length; i++)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 3),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('${i + 1}. ',
                                style: const TextStyle(fontWeight: FontWeight.bold)),
                            Expanded(child: Text(tr(spec.steps[i]))),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
              if (redirect != null) ...[
                const SizedBox(height: 12),
                Text(tr('Endereço de retorno (Redirect URI)'), style: muted),
                const SizedBox(height: 4),
                Row(children: [
                  Expanded(
                    child: SelectableText(redirect,
                        style: const TextStyle(fontFamily: 'monospace', fontSize: 13)),
                  ),
                  IconButton(
                    tooltip: tr('Copiar'),
                    icon: const Icon(Icons.copy, size: 18),
                    onPressed: () {
                      Clipboard.setData(ClipboardData(text: redirect));
                      _snack(tr('Copiado.'));
                    },
                  ),
                ]),
              ],
              const SizedBox(height: 16),
              for (final f in spec.fields) ...[
                TextField(
                  controller: _ctrls[f.key],
                  obscureText: f.secret,
                  decoration: InputDecoration(
                    labelText: tr(f.label),
                    hintText: f.secret && _status['configured'] == true
                        ? tr('Guardado no PC (deixe vazio para manter)')
                        : (f.hint == null ? null : tr(f.hint!)),
                    border: const OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
              ],
              Row(children: [
                if (connected || _status['configured'] == true)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _busy ? null : _logout,
                      icon: const Icon(Icons.logout),
                      label: Text(connected ? tr('Desconectar') : tr('Esquecer')),
                    ),
                  ),
                if (connected || _status['configured'] == true)
                  const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _busy ? null : _connect,
                    icon: _busy
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2))
                        : Icon(spec.icon),
                    label: Text(connected ? tr('Conectar de novo') : tr(spec.connectLabel)),
                  ),
                ),
              ]),
              if (connected && spec.connectedPage != null) ...[
                const SizedBox(height: 12),
                FilledButton.icon(
                  onPressed: () => Navigator.of(context)
                      .push(MaterialPageRoute(builder: spec.connectedPage!)),
                  icon: const Icon(Icons.open_in_full),
                  label: Text(tr(spec.connectedPageLabel ?? 'Abrir')),
                ),
              ],
              if (spec.browserLogin) ...[
                const SizedBox(height: 8),
                Text(tr('O login abre no navegador do PC, mesmo quando você toca aqui no celular.'),
                    style: muted),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _statusCard() {
    return Watch((context) {
      final live = Injector.get<SignalRService>().stateOf(spec.stateEvent);
      final connected = live['connected'] == true || _status['connected'] == true;
      final pending = _status['loginPending'] == true;
      final color = connected ? Colors.green : (pending ? Colors.amber : Colors.blueGrey);
      final account = (live['account'] ?? _status['account']) as String?;
      final extra = connected ? spec.summary?.call(live) : null;
      final text = connected
          ? '${tr('Conectado')}${account != null ? ' — $account' : ''}${extra != null ? ' · $extra' : ''}'
          : pending
              ? tr('Aguardando o login…')
              : (_status['message'] as String? ?? tr('Desconectado.'));
      return Card(
        color: color.withValues(alpha: 0.15),
        child: ListTile(
          leading: Icon(
              connected ? Icons.check_circle : (pending ? Icons.hourglass_top : Icons.link_off),
              color: color),
          title: Text(text),
        ),
      );
    });
  }

  Widget _deviceCodeCard() {
    final code = '${_status['userCode']}';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(children: [
          Text(tr('Confirme este código na página que abriu no PC:')),
          const SizedBox(height: 8),
          SelectableText(code,
              style: const TextStyle(
                  fontSize: 32, fontWeight: FontWeight.bold, letterSpacing: 4)),
          const SizedBox(height: 4),
          Text('${_status['verificationUri'] ?? ''}',
              style: TextStyle(
                  fontSize: 12,
                  color: Theme.of(context).colorScheme.onSurfaceVariant)),
        ]),
      ),
    );
  }
}
