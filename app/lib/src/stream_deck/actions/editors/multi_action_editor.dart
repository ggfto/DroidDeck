import 'package:droiddeck/src/studio/tally_target_field.dart';
import 'dart:async';
import 'dart:convert';

import 'package:droiddeck/core/core.dart';

import 'package:droiddeck/src/stream_deck/actions/editors/action_editor.dart';
import 'package:flutter/material.dart';

/// Multi-ação: passos em sequência, cada um com uma espera antes.
///
/// Passo no editor: {type: hotkey|launch_app|mute, param, delayMs, _k (chave estável)}.
/// No botão vira o JSON [{type, parameters, delayMs}] que o PC executa.
class MultiActionEditor extends StatefulWidget {
  final ActionEditorArgs args;

  const MultiActionEditor({super.key, required this.args});

  @override
  State<MultiActionEditor> createState() => _MultiActionEditorState();
}

class _MultiActionEditorState extends State<MultiActionEditor> {
  List<Map<String, dynamic>> _steps = [];
  int _seq = 0;

  @override
  void initState() {
    super.initState();
    _steps = _decode(widget.args.initial['steps']);
    WidgetsBinding.instance.addPostFrameCallback((_) => _emit());
  }

  void _emit() => widget.args.onChanged({'steps': _encode(_steps)});

  void _update(VoidCallback change) {
    setState(change);
    _emit();
  }

  List<Map<String, dynamic>> _decode(String? json) {
    if (json == null || json.isEmpty) return [];
    try {
      final list = jsonDecode(json) as List;
      return list.map<Map<String, dynamic>>((e) {
        final m = e as Map<String, dynamic>;
        final t = m['type'] as String? ?? 'hotkey';
        final p = (m['parameters'] as Map?)?.cast<String, dynamic>() ?? {};
        // Tipos que o editor não edita (vindos do Stream Deck, de versões novas) passam
        // intactos: só a espera e a condição mudam.
        String editorType = 'other';
        String param = '';
        if (t == 'hotkey') {
          editorType = t;
          param = p['keys']?.toString() ?? '';
        } else if (t == 'launch_app') {
          editorType = t;
          param = p['path']?.toString() ?? '';
        } else if (t == 'mixer' && p['processName'] != null) {
          editorType = 'mute';
          param = p['processName']?.toString() ?? '';
        }
        final cond = (m['if'] as Map?)?.cast<String, dynamic>();
        return {
          'type': editorType,
          'param': param,
          'rawType': t,
          'raw': p,
          'delayMs': (m['delayMs'] as num?)?.toInt() ?? 0,
          'check': cond?['check'] as String? ?? '',
          'value': cond?['value'] as String? ?? '',
          'negate': cond?['negate'] == true,
          '_k': _seq++,
        };
      }).toList();
    } catch (_) {
      return [];
    }
  }

  String _encode(List<Map<String, dynamic>> steps) {
    final out = steps.map((s) {
      final type = s['type'] as String? ?? 'hotkey';
      final param = (s['param'] as String? ?? '').trim();
      final delayMs = (s['delayMs'] as int?) ?? 0;
      final Map<String, dynamic> step;
      if (type == 'other') {
        step = {
          'type': s['rawType'],
          'parameters': s['raw'],
          'delayMs': delayMs,
        };
      } else if (type == 'mute') {
        step = {
          'type': 'mixer',
          'parameters': {'operation': 'toggleMute', 'processName': param},
          'delayMs': delayMs,
        };
      } else if (type == 'launch_app') {
        step = {
          'type': 'launch_app',
          'parameters': {'path': param},
          'delayMs': delayMs,
        };
      } else {
        step = {
          'type': 'hotkey',
          'parameters': {'keys': param},
          'delayMs': delayMs,
        };
      }
      final check = s['check'] as String? ?? '';
      final value = (s['value'] as String? ?? '').trim();
      if (check.isNotEmpty) {
        step['if'] = {
          'check': check,
          if (value.isNotEmpty) 'value': value,
          if (s['negate'] == true) 'negate': true,
        };
      }
      return step;
    }).toList();
    return jsonEncode(out);
  }

  /// Condições que o PC sabe olhar (ver DroidDeck/Services/Conditions.cs).
  static Map<String, String> get _checks => {
    '': tr('Sempre'),
    'obs_streaming': tr('A live do OBS estiver no ar'),
    'obs_recording': tr('O OBS estiver gravando'),
    'discord_mute': tr('O microfone do Discord estiver mudo'),
    'discord_deaf': tr('O áudio do Discord estiver desativado'),
    'toggle_on': tr('Um botão de dois estados estiver ligado'),
    'app_running': tr('Um app estiver aberto'),
    'app_focused': tr('Um app estiver em foco'),
    'time_between': tr('For entre dois horários'),
    'weekday': tr('For um destes dias da semana'),
    'tally_program': tr('Um alvo do tally estiver no ar'),
    'tally_preview': tr('Um alvo do tally estiver em preview'),
  };

  static String? _valueLabel(String check) => switch (check) {
    'toggle_on' => tr('Id do botão'),
    'app_running' || 'app_focused' => tr('Processo (sem .exe)'),
    'time_between' => tr('Horário (ex.: 08:00-18:00)'),
    'weekday' => tr('Dias (0 = domingo; ex.: 1-5)'),
    _ => null,
  };

  /// Botões de dois estados de todos os perfis ("Perfil · Rótulo").
  Widget _toggleButtonPicker(Map<String, dynamic> step) {
    final options = <String, String>{
      for (final p in widget.args.profiles)
        for (final b in p.buttons.where((b) => b.toggle))
          b.id: '${p.name} · ${b.label?.isNotEmpty == true ? b.label : b.id}',
    };
    final value = step['value'] as String? ?? '';
    if (options.isEmpty) {
      return Text(
        tr('Nenhum botão de dois estados ainda.'),
        style: Theme.of(context).textTheme.bodySmall,
      );
    }
    return DropdownButtonFormField<String>(
      initialValue: options.containsKey(value) ? value : null,
      isDense: true,
      isExpanded: true,
      decoration: InputDecoration(
        labelText: tr('Botão'),
        border: const OutlineInputBorder(),
      ),
      items: [
        for (final e in options.entries)
          DropdownMenuItem(
            value: e.key,
            child: Text(e.value, overflow: TextOverflow.ellipsis),
          ),
      ],
      onChanged: (v) => _update(() => step['value'] = v ?? ''),
    );
  }

  Widget _condition(Map<String, dynamic> step) {
    final k = step['_k'];
    final check = step['check'] as String? ?? '';
    final valueLabel = _valueLabel(check);
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: DropdownButtonFormField<String>(
                initialValue: _checks.containsKey(check) ? check : '',
                isDense: true,
                isExpanded: true,
                decoration: InputDecoration(
                  labelText: tr('Só se'),
                  border: const OutlineInputBorder(),
                ),
                items: [
                  for (final e in _checks.entries)
                    DropdownMenuItem(
                      value: e.key,
                      child: Text(e.value, overflow: TextOverflow.ellipsis),
                    ),
                ],
                onChanged: (v) => _update(() {
                  step['check'] = v ?? '';
                  step['value'] = '';
                }),
              ),
            ),
            if (check.isNotEmpty) ...[
              const SizedBox(width: 8),
              FilterChip(
                label: Text(tr('Não')),
                tooltip: tr(
                  'Inverte: o passo roda quando a condição NÃO vale.',
                ),
                selected: step['negate'] == true,
                onSelected: (v) => _update(() => step['negate'] = v),
              ),
            ],
          ],
        ),
        if (check == 'toggle_on') ...[
          const SizedBox(height: 8),
          _toggleButtonPicker(step),
        ] else if (check == 'tally_program' || check == 'tally_preview') ...[
          const SizedBox(height: 8),
          TallyTargetField(
            value: step['value'] as String?,
            onChanged: (id, _) => _update(() => step['value'] = id),
          ),
        ] else if (valueLabel != null) ...[
          const SizedBox(height: 8),
          TextFormField(
            key: ValueKey('v$k$check'),
            initialValue: step['value'] as String? ?? '',
            decoration: InputDecoration(
              labelText: valueLabel,
              isDense: true,
              border: const OutlineInputBorder(),
            ),
            onChanged: (v) {
              step['value'] = v;
              _emit();
            },
          ),
        ],
      ],
    );
  }

  String _paramLabel(String type) {
    switch (type) {
      case 'launch_app':
        return tr('Caminho do app (.exe)');
      case 'mute':
        return tr('Nome do app (processo)');
      default:
        return tr('Teclas (ex.: ^C = Ctrl+C)');
    }
  }

  /// Grava o que for digitado no PC e acrescenta os passos.
  Future<void> _record() async {
    final client = Injector.get<DroidDeckClient>();
    try {
      await client.postJson('/api/macro/start');
    } catch (_) {
      _snack(
        tr('Não foi possível gravar (só funciona no configurador do PC).'),
      );
      return;
    }
    if (!mounted) return;
    final stepsJson = await showDialog<String>(
      context: context,
      barrierDismissible: false,
      builder: (_) => const _RecordingDialog(),
    );
    if (stepsJson == null || !mounted) return;
    final recorded = _decode(stepsJson);
    if (recorded.isEmpty) {
      _snack(tr('Nenhuma tecla gravada.'));
      return;
    }
    _update(() => _steps.addAll(recorded));
    _snack(tr('{n} passo(s) gravado(s).', {'n': recorded.length}));
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  @override
  Widget build(BuildContext context) {
    final remote = widget.args.remote;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          tr('Passos (executados em sequência):'),
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),
        ..._steps.map((step) {
          final k = step['_k'];
          return Card(
            margin: const EdgeInsets.only(bottom: 8),
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: Column(
                children: [
                  Row(
                    children: [
                      if (step['type'] == 'other')
                        Expanded(
                          child: InputDecorator(
                            decoration: InputDecoration(
                              labelText: tr('Tipo'),
                              isDense: true,
                              border: const OutlineInputBorder(),
                            ),
                            child: Text(step['rawType'] as String? ?? ''),
                          ),
                        )
                      else
                        Expanded(
                          child: DropdownButtonFormField<String>(
                            value: step['type'] as String,
                            isDense: true,
                            decoration: InputDecoration(
                              labelText: tr('Tipo'),
                              border: const OutlineInputBorder(),
                            ),
                            items: [
                              if (!remote) ...[
                                DropdownMenuItem(
                                  value: 'hotkey',
                                  child: Text(tr('Atalho')),
                                ),
                                DropdownMenuItem(
                                  value: 'launch_app',
                                  child: Text(tr('Abrir app')),
                                ),
                              ],
                              DropdownMenuItem(
                                value: 'mute',
                                child: Text(tr('Mutar app')),
                              ),
                            ],
                            onChanged: (v) =>
                                _update(() => step['type'] = v ?? 'hotkey'),
                          ),
                        ),
                      IconButton(
                        icon: const Icon(
                          Icons.delete_outline,
                          color: Colors.redAccent,
                        ),
                        onPressed: () => _update(() => _steps.remove(step)),
                      ),
                    ],
                  ),
                  if (step['type'] != 'other') ...[
                    const SizedBox(height: 8),
                    TextFormField(
                      key: ValueKey('p$k'),
                      initialValue: step['param'] as String? ?? '',
                      decoration: InputDecoration(
                        labelText: _paramLabel(step['type'] as String),
                        isDense: true,
                        border: const OutlineInputBorder(),
                      ),
                      onChanged: (v) {
                        step['param'] = v;
                        _emit();
                      },
                    ),
                  ],
                  const SizedBox(height: 8),
                  TextFormField(
                    key: ValueKey('d$k'),
                    initialValue: (step['delayMs'] as int).toString(),
                    keyboardType: TextInputType.number,
                    decoration: InputDecoration(
                      labelText: tr('Esperar antes (ms)'),
                      isDense: true,
                      border: const OutlineInputBorder(),
                    ),
                    onChanged: (v) {
                      step['delayMs'] = int.tryParse(v) ?? 0;
                      _emit();
                    },
                  ),
                  const SizedBox(height: 8),
                  _condition(step),
                ],
              ),
            ),
          );
        }),
        Wrap(
          spacing: 8,
          children: [
            TextButton.icon(
              onPressed: () => _update(
                () => _steps.add({
                  'type': remote ? 'mute' : 'hotkey',
                  'param': '',
                  'delayMs': 0,
                  'check': '',
                  'value': '',
                  'negate': false,
                  '_k': _seq++,
                }),
              ),
              icon: const Icon(Icons.add),
              label: Text(tr('Adicionar passo')),
            ),
            // Gravar lê o teclado do PC: só no configurador, no próprio PC.
            if (!remote)
              TextButton.icon(
                onPressed: _record,
                icon: const Icon(
                  Icons.fiber_manual_record,
                  color: Colors.redAccent,
                ),
                label: Text(tr('Gravar do teclado')),
              ),
          ],
        ),
      ],
    );
  }
}

/// Enquanto grava: mostra quantas teclas já vieram e para no botão (ou sozinho no tempo máximo).
class _RecordingDialog extends StatefulWidget {
  const _RecordingDialog();

  @override
  State<_RecordingDialog> createState() => _RecordingDialogState();
}

class _RecordingDialogState extends State<_RecordingDialog> {
  final _client = Injector.get<DroidDeckClient>();
  Timer? _poll;
  int _keys = 0;
  int _seconds = 0;
  int _max = 60;
  bool _stopping = false;

  @override
  void initState() {
    super.initState();
    _poll = Timer.periodic(const Duration(milliseconds: 500), (_) async {
      try {
        final s = Map<String, dynamic>.from(
          await _client.getJson('/api/macro') as Map,
        );
        if (!mounted) return;
        setState(() {
          _keys = (s['keys'] as num?)?.toInt() ?? 0;
          _seconds = (s['seconds'] as num?)?.toInt() ?? 0;
          _max = (s['maxSeconds'] as num?)?.toInt() ?? 60;
        });
        if (s['recording'] != true) _stop();
      } catch (_) {}
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _stop() async {
    if (_stopping) return;
    _stopping = true;
    _poll?.cancel();
    String? steps;
    try {
      final r = Map<String, dynamic>.from(
        await _client.postJson('/api/macro/stop') as Map,
      );
      steps = r['steps'] as String?;
    } catch (_) {}
    if (mounted) Navigator.of(context).pop(steps);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Row(
        children: [
          const Icon(Icons.fiber_manual_record, color: Colors.redAccent),
          const SizedBox(width: 8),
          Text(tr('Gravando')),
        ],
      ),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            tr(
              'Vá para o programa e digite a sequência no teclado do PC. '
              'Pausas de mais de meio segundo viram espera entre os passos.',
            ),
          ),
          const SizedBox(height: 16),
          Text(
            tr('{keys} tecla(s) · {seconds}s de {max} s', {
              'keys': _keys,
              'seconds': _seconds,
              'max': _max,
            }),
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
        ],
      ),
      actions: [
        FilledButton.icon(
          onPressed: _stop,
          icon: const Icon(Icons.stop),
          label: Text(tr('Parar')),
        ),
      ],
    );
  }
}
