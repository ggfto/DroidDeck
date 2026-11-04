import 'dart:async';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

const tallyEvent = 'ReceiveTallyState';
const tallyTargetPrefKey = 'tally_target';

/// Alvo do sinal manual: é o único que se liga e desliga por aqui.
const tallyManualTarget = 'manual/onair';

/// O que a tela de tally mostra. Sem conexão é [noSignal], nunca [idle]: quem está diante da
/// câmera não pode achar que está fora do ar só porque o Wi-Fi caiu.
enum TallyMode { program, preview, idle, noSignal }

/// Estado de um alvo, a partir do que o PC publicou.
TallyMode tallyModeOf(Map<String, dynamic> state, String? target, {required bool linked}) {
  if (!linked || target == null) return TallyMode.noSignal;
  final targets = state['targets'];
  if (targets is! Map) return TallyMode.noSignal;
  final entry = targets[target];
  if (entry is! Map) return TallyMode.noSignal;
  return switch (entry['state']) {
    'program' => TallyMode.program,
    'preview' => TallyMode.preview,
    'idle' => TallyMode.idle,
    _ => TallyMode.noSignal,
  };
}

/// Cores de transmissão: vermelho no ar, verde em preview. Sempre com texto, para quem não
/// distingue vermelho de verde.
const tallyProgramColor = Color(0xFFC62828);
const tallyPreviewColor = Color(0xFF1B7F3B);

/// Celular em tela cheia como luz de "no ar" ou tally de câmera. Cada aparelho acompanha o
/// próprio alvo, guardado nele.
class TallyPage extends StatefulWidget {
  /// Alvo do botão que abriu a tela. Sem ele, vale o último escolhido neste aparelho.
  final String? target;

  const TallyPage({super.key, this.target});

  @override
  State<TallyPage> createState() => _TallyPageState();
}

class _TallyPageState extends State<TallyPage> {
  String? _target;
  bool _ready = false;
  bool _chrome = false;
  Timer? _hideChrome;

  SignalRService get _signalR => Injector.get<SignalRService>();

  @override
  void initState() {
    super.initState();
    _signalR.listenState(tallyEvent);
    _seed();
    _restoreTarget();
    _keepAwake(true);
  }

  @override
  void dispose() {
    _hideChrome?.cancel();
    _keepAwake(false);
    super.dispose();
  }

  void _keepAwake(bool on) {
    // Sem suporte no aparelho (navegador antigo): a tela segue, só pode apagar sozinha.
    WakelockPlus.toggle(enable: on).catchError((_) {});
  }

  Future<void> _seed() async {
    try {
      final state = await Injector.get<DroidDeckClient>().getActionState('tally');
      if (state != null && mounted) _signalR.seedState(tallyEvent, state);
    } catch (_) {
      // Sem resposta: fica em "sem sinal" até o hub mandar o estado.
    }
  }

  Future<void> _restoreTarget() async {
    var target = widget.target;
    if (target == null || target.isEmpty) {
      final prefs = await SharedPreferences.getInstance();
      target = prefs.getString(tallyTargetPrefKey);
    }
    if (!mounted) return;
    setState(() {
      _target = target;
      _ready = true;
    });
    if (target == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _pickTarget());
    } else if (target == tallyManualTarget) {
      _showChrome();
    }
  }

  Future<void> _pickTarget() async {
    final picked = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _TargetPicker(),
    );
    if (picked == null || !mounted) return;
    setState(() => _target = picked);
    if (picked == tallyManualTarget) _showChrome();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(tallyTargetPrefKey, picked);
  }

  void _toggleChrome() {
    if (_chrome) {
      _hideChrome?.cancel();
      setState(() => _chrome = false);
    } else {
      _showChrome();
    }
  }

  /// Os controles somem sozinhos: uma tela de corredor não fica com botão à vista, e um
  /// toque sem querer não liga o ON AIR (são dois toques: mostrar e apertar).
  void _showChrome() {
    _hideChrome?.cancel();
    setState(() => _chrome = true);
    _hideChrome = Timer(const Duration(seconds: 5), () {
      if (mounted) setState(() => _chrome = false);
    });
  }

  Future<void> _setManual(bool on) async {
    _showChrome();
    try {
      await Injector.get<DroidDeckClient>().executeAction(DeckAction(
        type: 'tally',
        parameters: {'operation': on ? 'manual_on' : 'manual_off'},
      ));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(tr('Não foi possível falar com o PC.'))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    return Watch((context) {
      final state = _signalR.stateOf(tallyEvent);
      final linked = _signalR.connectionStatus.value == 'Connected';
      final mode = tallyModeOf(state, _target, linked: linked);
      final targets = state['targets'];
      final entry = targets is Map ? targets[_target] : null;
      final label = entry is Map ? '${entry['label'] ?? ''}' : '';
      // Sem sinal não há como saber o estado, então não oferece ligar nem desligar.
      final canSwitch = _target == tallyManualTarget && mode != TallyMode.noSignal;
      return Scaffold(
        backgroundColor: Colors.black,
        body: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: _toggleChrome,
          child: AnimatedContainer(
            duration: Duration(milliseconds: reduce ? 0 : 90),
            color: switch (mode) {
              TallyMode.program => tallyProgramColor,
              TallyMode.preview => tallyPreviewColor,
              TallyMode.idle => const Color(0xFF101114),
              TallyMode.noSignal => const Color(0xFF3A3A3A),
            },
            child: SafeArea(
              child: Stack(
                children: [
                  if (_ready)
                    Positioned.fill(
                      child: _TallyFace(
                        mode: mode,
                        label: label,
                        hasTarget: _target != null,
                        hint: canSwitch && !_chrome ? tr('Toque na tela para ligar ou desligar.') : null,
                      ),
                    ),
                  if (_chrome && canSwitch)
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 16,
                      child: Center(
                        child: FilledButton.icon(
                          style: FilledButton.styleFrom(
                            backgroundColor: Colors.white,
                            foregroundColor: Colors.black,
                            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                          ),
                          onPressed: () => _setManual(mode != TallyMode.program),
                          icon: Icon(mode == TallyMode.program ? Icons.stop : Icons.sensors),
                          label: Text(mode == TallyMode.program ? tr('Desligar o ON AIR') : tr('Ligar o ON AIR')),
                        ),
                      ),
                    ),
                  if (_chrome || _target == null)
                    Positioned(
                      top: 0,
                      left: 0,
                      right: 0,
                      child: Row(
                        children: [
                          IconButton(
                            icon: const Icon(Icons.arrow_back, color: Colors.white),
                            tooltip: tr('Voltar'),
                            onPressed: () => Navigator.of(context).maybePop(),
                          ),
                          const Spacer(),
                          TextButton.icon(
                            onPressed: _pickTarget,
                            icon: const Icon(Icons.tune, color: Colors.white),
                            label: Text(tr('Trocar'), style: const TextStyle(color: Colors.white)),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      );
    });
  }
}

class _TallyFace extends StatelessWidget {
  final TallyMode mode;
  final String label;
  final bool hasTarget;
  final String? hint;

  const _TallyFace({required this.mode, required this.label, required this.hasTarget, this.hint});

  @override
  Widget build(BuildContext context) {
    final (title, detail) = switch (mode) {
      TallyMode.program => (tr('NO AR'), label),
      TallyMode.preview => (tr('PREVIEW'), label),
      TallyMode.idle => (tr('FORA DO AR'), label),
      TallyMode.noSignal => (
          tr('SEM SINAL'),
          hasTarget
              ? tr('Sem resposta do PC ou do programa. O estado é desconhecido.')
              : tr('Escolha o que acompanhar.'),
        ),
    };
    final fg = mode == TallyMode.idle ? Colors.white70 : Colors.white;
    return Semantics(
      liveRegion: true,
      label: detail.isEmpty ? title : '$title. $detail',
      excludeSemantics: true,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 48, 24, 72),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Flexible(
              flex: 3,
              child: FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  title,
                  maxLines: 1,
                  style: TextStyle(
                    color: fg,
                    fontSize: 200,
                    fontWeight: FontWeight.w900,
                    height: 1,
                  ),
                ),
              ),
            ),
            if (detail.isNotEmpty) ...[
              const SizedBox(height: 16),
              Flexible(
                child: Text(
                  detail,
                  textAlign: TextAlign.center,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(color: fg, fontSize: 22, fontWeight: FontWeight.w600),
                ),
              ),
            ],
            if (hint != null) ...[
              const SizedBox(height: 8),
              Text(hint!, textAlign: TextAlign.center, style: TextStyle(color: fg, fontSize: 14)),
            ],
          ],
        ),
      ),
    );
  }
}

/// Lista do que está conectado agora, do PC.
class _TargetPicker extends StatefulWidget {
  const _TargetPicker();

  @override
  State<_TargetPicker> createState() => _TargetPickerState();
}

class _TargetPickerState extends State<_TargetPicker> {
  List<Map<String, dynamic>>? _options;
  bool _failed = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final list = await Injector.get<DroidDeckClient>().getJson('/api/tally/targets') as List;
      if (!mounted) return;
      setState(() => _options = list.map((e) => Map<String, dynamic>.from(e as Map)).toList());
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final options = _options;
    return SafeArea(
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.7),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              title: Text(tr('O que acompanhar'), style: const TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(tr('A lista traz só o que está conectado agora.')),
            ),
            if (_failed)
              Padding(
                padding: const EdgeInsets.all(16),
                child: Text(tr('Não foi possível falar com o PC.')),
              )
            else if (options == null)
              const Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator())
            else
              Flexible(
                child: ListView(
                  shrinkWrap: true,
                  children: [
                    for (final o in options)
                      ListTile(
                        title: Text('${o['label']}'),
                        onTap: () => Navigator.of(context).pop('${o['value']}'),
                      ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}
