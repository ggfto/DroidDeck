import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';

/// Configuração da soundboard (MyInstants): escolhe o dispositivo de saída "cabo"
/// (que vai pro Discord/OBS via VB-Cable/Voicemeeter) e um "monitor" opcional (pra você
/// ouvir), além do volume. O backend toca os sons nesses dispositivos.
class SoundboardSettingsPage extends StatefulWidget {
  const SoundboardSettingsPage({super.key});

  @override
  State<SoundboardSettingsPage> createState() => _SoundboardSettingsPageState();
}

class _SoundboardSettingsPageState extends State<SoundboardSettingsPage> {
  bool _loading = true;
  bool _busy = false;
  List<Map<String, String>> _devices = [];
  String? _cableDeviceId;
  String? _monitorDeviceId;
  bool _monitorEnabled = false;
  double _volume = 100;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final client = Injector.get<DroidDeckClient>();
    try {
      final devices = await client.getSoundDevices();
      final cfg = await client.getSoundboardConfig();
      if (!mounted) return;
      setState(() {
        _devices = devices;
        if (cfg != null) {
          _cableDeviceId = (cfg['cableDeviceId'] as String?)?.isEmpty ?? true
              ? null
              : cfg['cableDeviceId'] as String?;
          _monitorDeviceId = (cfg['monitorDeviceId'] as String?)?.isEmpty ?? true
              ? null
              : cfg['monitorDeviceId'] as String?;
          _monitorEnabled = cfg['monitorEnabled'] == true;
          _volume = ((cfg['volume'] as num?)?.toDouble() ?? 100).clamp(0, 100);
        }
        _loading = false;
      });
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _snack(String m) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));
    }
  }

  Future<void> _save() async {
    setState(() => _busy = true);
    try {
      await Injector.get<DroidDeckClient>().saveSoundboardConfig(
        cableDeviceId: _cableDeviceId,
        monitorDeviceId: _monitorDeviceId,
        monitorEnabled: _monitorEnabled,
        volume: _volume.round(),
      );
      _snack('Configuração da soundboard salva.');
    } catch (_) {
      _snack('Não foi possível salvar (servidor acessível?).');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _stopTest() async {
    try {
      await Injector.get<DroidDeckClient>().stopSounds();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Soundboard')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 560),
                child: ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    _guideCard(),
                    const SizedBox(height: 16),
                    _deviceDropdown(
                      label: 'Saída "cabo" (vai pro Discord/OBS)',
                      helper:
                          'Ex.: "CABLE Input" (VB-Cable). Vazio = dispositivo padrão do Windows.',
                      value: _cableDeviceId,
                      allowNull: true,
                      onChanged: (v) => setState(() => _cableDeviceId = v),
                    ),
                    const SizedBox(height: 16),
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Tocar também no monitor'),
                      subtitle: const Text('Pra você ouvir o som (fone/alto-falante)'),
                      value: _monitorEnabled,
                      onChanged: (v) => setState(() => _monitorEnabled = v),
                    ),
                    if (_monitorEnabled) ...[
                      const SizedBox(height: 8),
                      _deviceDropdown(
                        label: 'Saída "monitor"',
                        helper: 'Seu fone/alto-falante.',
                        value: _monitorDeviceId,
                        allowNull: false,
                        onChanged: (v) => setState(() => _monitorDeviceId = v),
                      ),
                    ],
                    const SizedBox(height: 24),
                    Text('Volume: ${_volume.round()}%'),
                    Slider(
                      value: _volume,
                      min: 0,
                      max: 100,
                      divisions: 100,
                      label: '${_volume.round()}%',
                      onChanged: (v) => setState(() => _volume = v),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: _busy ? null : _stopTest,
                            icon: const Icon(Icons.stop),
                            label: const Text('Parar tudo'),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: ElevatedButton.icon(
                            onPressed: _busy ? null : _save,
                            icon: _busy
                                ? const SizedBox(
                                    width: 16,
                                    height: 16,
                                    child: CircularProgressIndicator(strokeWidth: 2))
                                : const Icon(Icons.save),
                            label: const Text('Salvar'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
    );
  }

  Widget _deviceDropdown({
    required String label,
    required String helper,
    required String? value,
    required bool allowNull,
    required ValueChanged<String?> onChanged,
  }) {
    final ids = _devices.map((d) => d['id']).toSet();
    final items = <DropdownMenuItem<String?>>[
      if (allowNull)
        const DropdownMenuItem<String?>(
            value: null, child: Text('Dispositivo padrão do Windows')),
      ..._devices.map((d) => DropdownMenuItem<String?>(
            value: d['id'],
            child: Text(d['name'] ?? d['id'] ?? '',
                overflow: TextOverflow.ellipsis),
          )),
    ];
    return DropdownButtonFormField<String?>(
      value: (value != null && ids.contains(value)) ? value : null,
      isExpanded: true,
      decoration: InputDecoration(
        labelText: label,
        helperText: helper,
        helperMaxLines: 3,
        border: const OutlineInputBorder(),
      ),
      items: items,
      onChanged: onChanged,
    );
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
        title: const Text('Como levar o som pro Discord / live'),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        children: [
          step('1', 'Instale o VB-Cable (grátis) e reinicie o PC.'),
          step('2', 'Aqui, escolha a saída "cabo" = "CABLE Input (VB-Audio Virtual Cable)".'),
          step('3', 'No Discord: Configurações de Voz → Dispositivo de entrada = "CABLE Output".'),
          step('4', 'No OBS: adicione uma "Captura de Entrada de Áudio" do "CABLE Output".'),
          const SizedBox(height: 8),
          const Text(
            'Quer sua voz + os sons juntos no Discord? Use o Voicemeeter pra mixar mic + soundboard '
            'numa saída virtual só. Na live/OBS mic e cabo entram como fontes separadas.',
            style: TextStyle(fontSize: 12, color: Colors.grey),
          ),
        ],
      ),
    );
  }
}
