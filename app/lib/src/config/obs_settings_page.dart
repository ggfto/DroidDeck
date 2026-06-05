import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';

import 'obs_qr.dart';
import 'qr_scan_page.dart';

/// Configuração do plugin do OBS: conecta no obs-websocket (embutido no OBS 28+).
class ObsSettingsPage extends StatefulWidget {
  const ObsSettingsPage({super.key});

  @override
  State<ObsSettingsPage> createState() => _ObsSettingsPageState();
}

class _ObsSettingsPageState extends State<ObsSettingsPage> {
  final _hostCtrl = TextEditingController(text: 'localhost');
  final _portCtrl = TextEditingController(text: '4455');
  final _passCtrl = TextEditingController();
  bool _busy = false;
  bool _obscure = true;

  @override
  void initState() {
    super.initState();
    Injector.get<DroidDeckClient>().getObsState().then((s) {
      if (s != null && mounted) {
        Injector.get<SignalRService>().obsState.value =
            Map<String, dynamic>.from(s);
      }
    }).catchError((_) {});
  }

  @override
  void dispose() {
    _hostCtrl.dispose();
    _portCtrl.dispose();
    _passCtrl.dispose();
    super.dispose();
  }

  void _snack(String m) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
    }
  }

  Future<void> _save() async {
    setState(() => _busy = true);
    try {
      final port = int.tryParse(_portCtrl.text.trim()) ?? 4455;
      final host = _hostCtrl.text.trim().isEmpty ? 'localhost' : _hostCtrl.text.trim();
      await Injector.get<DroidDeckClient>()
          .setObsConfig(host, port, _passCtrl.text.isEmpty ? null : _passCtrl.text);
      _snack('Config salva. Agora toque em "Conectar".');
    } catch (_) {
      _snack('Não foi possível salvar (servidor acessível?).');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _connect() async {
    setState(() => _busy = true);
    try {
      final st = await Injector.get<DroidDeckClient>().connectObs();
      Injector.get<SignalRService>().obsState.value =
          Map<String, dynamic>.from(st);
      _snack('OBS conectado!');
    } catch (e) {
      _snack(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Lê o QR "Mostrar Informações de Conexão" do OBS e já configura + conecta.
  Future<void> _scanObsQr() async {
    final raw = await Navigator.of(context).push<String>(
      MaterialPageRoute(
        builder: (_) => const QrScanPage(
          acceptPrefixes: ['obsws://', 'obswss://', 'obswebsocket|'],
          title: 'OBS — aponte para o QR',
          hint:
              'No OBS: Ferramentas → Config. do Servidor WebSocket → Mostrar Informações de Conexão',
        ),
      ),
    );
    if (raw == null || !mounted) return;
    final parsed = parseObsQr(raw);
    if (parsed == null) {
      _snack('QR do OBS não reconhecido.');
      return;
    }
    setState(() {
      _hostCtrl.text = parsed.host;
      _portCtrl.text = '${parsed.port}';
      _passCtrl.text = parsed.password ?? '';
      _busy = true;
    });
    // O OBS roda na mesma máquina do backend → host fica localhost; do QR
    // aproveitamos porta e senha. Salva e conecta de uma vez.
    try {
      await Injector.get<DroidDeckClient>()
          .setObsConfig(parsed.host, parsed.port, parsed.password);
      final st = await Injector.get<DroidDeckClient>().connectObs();
      Injector.get<SignalRService>().obsState.value =
          Map<String, dynamic>.from(st);
      _snack('OBS conectado pelo QR!');
    } catch (e) {
      _snack(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('OBS')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _statusCard(),
              const SizedBox(height: 16),
              _guideCard(),
              const SizedBox(height: 16),
              if (!kIsWeb) ...[
                ElevatedButton.icon(
                  onPressed: _busy ? null : _scanObsQr,
                  icon: const Icon(Icons.qr_code_scanner),
                  label: const Text('Ler QR do OBS'),
                  style: ElevatedButton.styleFrom(
                      minimumSize: const Size.fromHeight(48)),
                ),
                const SizedBox(height: 4),
                const Text(
                  'Jeito mais fácil: no OBS, "Mostrar Informações de Conexão" e aponte a câmera.',
                  style: TextStyle(fontSize: 12, color: Colors.grey),
                ),
                const SizedBox(height: 12),
                const Row(children: [
                  Expanded(child: Divider()),
                  Padding(
                    padding: EdgeInsets.symmetric(horizontal: 8),
                    child: Text('ou manualmente',
                        style: TextStyle(color: Colors.grey, fontSize: 12)),
                  ),
                  Expanded(child: Divider()),
                ]),
                const SizedBox(height: 12),
              ],
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _hostCtrl,
                      decoration: const InputDecoration(
                          labelText: 'Host', border: OutlineInputBorder()),
                    ),
                  ),
                  const SizedBox(width: 12),
                  SizedBox(
                    width: 110,
                    child: TextField(
                      controller: _portCtrl,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(
                          labelText: 'Porta', border: OutlineInputBorder()),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _passCtrl,
                obscureText: _obscure,
                decoration: InputDecoration(
                  labelText: 'Senha (obs-websocket)',
                  border: const OutlineInputBorder(),
                  suffixIcon: IconButton(
                    icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: _busy ? null : _save,
                      icon: const Icon(Icons.save),
                      label: const Text('Salvar'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: ElevatedButton.icon(
                      onPressed: _busy ? null : _connect,
                      icon: _busy
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2))
                          : const Icon(Icons.link),
                      label: const Text('Conectar'),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              const Text(
                'O OBS precisa estar aberto. A conexão é local (ws://host:porta).',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _statusCard() {
    return Watch((context) {
      final s = Injector.get<SignalRService>().obsState.value;
      final connected = s['connected'] == true;
      final color = connected ? Colors.green : Colors.blueGrey;
      final scene = s['currentScene'];
      final rec = s['recording'] == true;
      final live = s['streaming'] == true;
      final text = connected
          ? 'Conectado${scene != null && '$scene'.isNotEmpty ? ' — cena "$scene"' : ''}'
              '${rec ? ' · GRAVANDO' : ''}${live ? ' · AO VIVO' : ''}'
          : 'Desconectado. Abra o OBS, ative o obs-websocket e toque em Conectar.';
      return Card(
        color: color.withValues(alpha: 0.15),
        child: ListTile(
          leading: Icon(connected ? Icons.check_circle : Icons.link_off, color: color),
          title: Text(text),
        ),
      );
    });
  }

  Widget _guideCard() {
    Widget step(String n, String t) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 3),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('$n. ', style: const TextStyle(fontWeight: FontWeight.bold)),
              Expanded(child: Text(t)),
            ],
          ),
        );
    return Card(
      child: ExpansionTile(
        leading: const Icon(Icons.help_outline),
        title: const Text('Como ativar o obs-websocket'),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        children: [
          step('1', 'No OBS (28+): menu Ferramentas → Configurações do Servidor WebSocket.'),
          step('2', 'Marque "Ativar Servidor WebSocket". Anote a Porta (padrão 4455).'),
          step('3', 'Se "Ativar Autenticação" estiver ligado, clique em "Mostrar Informações de Conexão" e copie a Senha.'),
          const SizedBox(height: 8),
          const Text('Sem senha? Deixe o campo vazio.',
              style: TextStyle(fontSize: 12, color: Colors.grey)),
        ],
      ),
    );
  }
}
