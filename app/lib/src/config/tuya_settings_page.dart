import 'dart:async';
import 'dart:convert';

import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:flutter/material.dart';
import 'package:signals_flutter/signals_flutter.dart';

/// Configuração do plugin Tuya / Smart Life — cobre também as marcas que são rebrand
/// da Tuya (Nova Digital, Positivo, RSmart, Elgin, Geonav...).
///
/// O pareamento é por QR e não exige conta de desenvolvedor: o usuário informa o
/// "Código de usuário" do app e escaneia. A ressalva importante está no aviso da tela —
/// só os apps **Smart Life** e **Tuya Smart** aceitam o scan; apps de marca recusam.
class TuyaSettingsPage extends StatefulWidget {
  const TuyaSettingsPage({super.key});

  @override
  State<TuyaSettingsPage> createState() => _TuyaSettingsPageState();
}

class _TuyaSettingsPageState extends State<TuyaSettingsPage> {
  final _userCodeCtrl = TextEditingController();

  bool _busy = false;
  String? _qrBase64;
  int _secondsLeft = 0;
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    Injector.get<DroidDeckClient>().getTuyaState().then((s) {
      if (s != null && mounted) {
        Injector.get<SignalRService>().tuyaState.value =
            Map<String, dynamic>.from(s);
        setState(() => _userCodeCtrl.text = (s['userCode'] ?? '') as String);
      }
    }).catchError((_) {});
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    _userCodeCtrl.dispose();
    super.dispose();
  }

  void _snack(String m) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
    }
  }

  Future<void> _startPairing() async {
    final code = _userCodeCtrl.text.trim();
    if (code.isEmpty) {
      _snack('Informe o código de usuário primeiro.');
      return;
    }

    setState(() => _busy = true);
    try {
      final r = await Injector.get<DroidDeckClient>().startTuyaPairing(code);
      setState(() {
        _qrBase64 = r['qrPng'] as String?;
        _secondsLeft = (r['expiresInSeconds'] as num?)?.toInt() ?? 120;
      });
      _startPolling();
    } catch (e) {
      _snack(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Consulta a cada 3s se o usuário já escaneou, e conta o tempo restante do QR.
  void _startPolling() {
    _pollTimer?.cancel();
    _pollTimer = Timer.periodic(const Duration(seconds: 3), (timer) async {
      if (!mounted) {
        timer.cancel();
        return;
      }

      setState(() => _secondsLeft = (_secondsLeft - 3).clamp(0, 999));

      try {
        final scanned = await Injector.get<DroidDeckClient>().pollTuyaPairing();
        if (!scanned) return;

        timer.cancel();
        final state = await Injector.get<DroidDeckClient>().getTuyaState();
        if (state != null && mounted) {
          Injector.get<SignalRService>().tuyaState.value =
              Map<String, dynamic>.from(state);
        }
        if (mounted) setState(() => _qrBase64 = null);
        _snack('Conta vinculada!');
      } catch (e) {
        // 410 = QR expirado: para o laço e deixa o usuário gerar outro.
        timer.cancel();
        if (mounted) setState(() => _qrBase64 = null);
        _snack(e.toString().replaceFirst('Exception: ', ''));
      }
    });
  }

  Future<void> _refreshDevices() async {
    setState(() => _busy = true);
    try {
      final state = await Injector.get<DroidDeckClient>().refreshTuyaDevices();
      Injector.get<SignalRService>().tuyaState.value =
          Map<String, dynamic>.from(state);
      _snack('Dispositivos atualizados.');
    } catch (e) {
      _snack(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = Injector.get<SignalRService>().tuyaState.watch(context);
    final paired = state['paired'] == true;
    final pushOn = state['push'] == true;
    final devices = (state['devices'] as List? ?? const []);

    return Scaffold(
      appBar: AppBar(title: const Text('Tuya / Smart Life')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _statusCard(paired, pushOn, devices.length),
              const SizedBox(height: 16),
              if (!paired || _qrBase64 != null) ...[
                _guideCard(),
                const SizedBox(height: 16),
                TextField(
                  controller: _userCodeCtrl,
                  decoration: const InputDecoration(
                    labelText: 'Código de usuário',
                    hintText: 'ex.: BxYZNPJ',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                ElevatedButton.icon(
                  onPressed: _busy ? null : _startPairing,
                  icon: const Icon(Icons.qr_code_2),
                  label: Text(_qrBase64 == null ? 'Gerar QR' : 'Gerar outro QR'),
                  style: ElevatedButton.styleFrom(
                      minimumSize: const Size.fromHeight(48)),
                ),
              ],
              if (_qrBase64 != null) ...[
                const SizedBox(height: 16),
                _qrCard(),
              ],
              if (paired) ...[
                const SizedBox(height: 16),
                _devicesCard(devices),
              ],
              const SizedBox(height: 16),
              _oemWarningCard(),
            ],
          ),
        ),
      ),
    );
  }

  Widget _statusCard(bool paired, bool pushOn, int deviceCount) {
    return Card(
      child: ListTile(
        leading: Icon(
          paired ? Icons.cloud_done : Icons.cloud_off,
          color: paired ? Colors.green : Colors.grey,
        ),
        title: Text(paired ? 'Conta vinculada' : 'Não vinculado'),
        subtitle: Text(paired
            ? '$deviceCount dispositivo(s) · estado ao vivo ${pushOn ? "ativo" : "reconectando..."}'
            : 'Vincule sua conta Smart Life para usar botões de casa inteligente.'),
      ),
    );
  }

  Widget _guideCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: const [
            Text('Onde achar o código de usuário',
                style: TextStyle(fontWeight: FontWeight.bold)),
            SizedBox(height: 8),
            Text('No app Smart Life (ou Tuya Smart):\n'
                '1. Aba "Eu" (canto inferior direito)\n'
                '2. Ícone de engrenagem (canto superior direito)\n'
                '3. "Conta e segurança"\n'
                '4. Role até o fim: "Código de usuário"'),
          ],
        ),
      ),
    );
  }

  Widget _qrCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            const Text('Escaneie com o app Smart Life',
                style: TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 4),
            const Text(
              'Aba "Home" → ícone de scan no topo direito.',
              style: TextStyle(fontSize: 12, color: Colors.grey),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            // O PNG vem pronto do backend (QRCoder): o app não tem lib de gerar QR.
            Image.memory(base64Decode(_qrBase64!), width: 240, height: 240),
            const SizedBox(height: 8),
            Text(
              _secondsLeft > 0
                  ? 'Expira em ${_secondsLeft}s'
                  : 'Expirado — gere outro',
              style: TextStyle(
                fontSize: 12,
                color: _secondsLeft > 20 ? Colors.grey : Colors.orange,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _devicesCard(List<dynamic> devices) {
    return Card(
      child: Column(
        children: [
          ListTile(
            title: const Text('Dispositivos'),
            trailing: IconButton(
              icon: const Icon(Icons.refresh),
              tooltip: 'Buscar novos dispositivos na conta',
              onPressed: _busy ? null : _refreshDevices,
            ),
          ),
          if (devices.isEmpty)
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 0, 16, 16),
              child: Text(
                'Nenhum dispositivo. Se acabou de adicionar um no app, toque em atualizar.',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
            ),
          ...devices.map((d) {
            final device = d as Map;
            final online = device['online'] == true;
            return ListTile(
              dense: true,
              leading: Icon(
                online ? Icons.lightbulb : Icons.lightbulb_outline,
                color: online ? Colors.amber : Colors.grey,
              ),
              title: Text('${device['name']}'),
              subtitle: Text(
                '${device['productName'] ?? device['category']} · '
                '${online ? "online" : "offline"}',
                style: const TextStyle(fontSize: 11),
              ),
            );
          }),
          const SizedBox(height: 8),
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: Text(
              'Atualizar consome cota da API da Tuya — use só ao adicionar aparelhos.',
              style: TextStyle(fontSize: 11, color: Colors.grey),
            ),
          ),
        ],
      ),
    );
  }

  Widget _oemWarningCard() {
    return Card(
      color: Colors.orange.withValues(alpha: 0.08),
      child: const Padding(
        padding: EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Usa app de marca?',
                style: TextStyle(fontWeight: FontWeight.bold)),
            SizedBox(height: 8),
            Text(
              'O scan só funciona nos apps Smart Life e Tuya Smart. Apps de marca '
              '(Nova Digital, Positivo, RSmart...) recusam com "please use the '
              'designated app to scan the code".\n\n'
              'Solução: remova o aparelho do app da marca e pareie de novo pelo '
              'Smart Life — é o mesmo hardware, funciona igual. Compartilhar o '
              'dispositivo não resolve.',
              style: TextStyle(fontSize: 12),
            ),
          ],
        ),
      ),
    );
  }
}
