import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/studio/tally_target_field.dart';
import 'package:flutter/material.dart';

/// Automações: um acontecimento no PC (horário, app abrindo ou fechando, volta da
/// suspensão, DroidDeck iniciando) aperta um botão do deck sozinho.
class TriggersPage extends StatefulWidget {
  const TriggersPage({super.key});

  @override
  State<TriggersPage> createState() => _TriggersPageState();
}

class _TriggersPageState extends State<TriggersPage> {
  final _client = Injector.get<DroidDeckClient>();
  List<Map<String, dynamic>> _triggers = [];
  List<DeckProfile> _profiles = [];
  bool _loading = true;
  String? _error;

  static Map<String, String> get events => {
        'schedule': tr('Num horário'),
        'app_start': tr('Quando um app abrir'),
        'app_exit': tr('Quando um app fechar'),
        'resume': tr('Quando o PC voltar da suspensão'),
        'startup': tr('Quando o DroidDeck iniciar'),
        'tally_on': tr('Quando um alvo do tally entrar no ar'),
        'tally_off': tr('Quando um alvo do tally sair do ar'),
      };

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final list = await _client.getJson('/api/triggers') as List;
      final profiles = await _client.getProfiles();
      if (!mounted) return;
      setState(() {
        _triggers = list.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        _profiles = profiles;
        _loading = false;
      });
    } catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = tr('O PC não respondeu.');
        });
      }
    }
  }

  Future<void> _save() async {
    try {
      await _client.putJson('/api/triggers', _triggers);
    } catch (e) {
      if (mounted) _snack(tr('Não deu para salvar: {reason}', {'reason': friendlyError(e)}));
    }
  }

  void _snack(String m) =>
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(m)));

  String _buttonName(Map<String, dynamic> t) {
    final p = _profiles.where((p) => p.id == t['profileId']).firstOrNull;
    final b = p?.buttons.where((b) => b.id == t['buttonId']).firstOrNull;
    if (p == null || b == null) return tr('(botão apagado)');
    return '${p.name} · ${b.label?.isNotEmpty == true ? b.label : tr('sem rótulo')}';
  }

  static List<String> get dayNames =>
      [tr('Dom'), tr('Seg'), tr('Ter'), tr('Qua'), tr('Qui'), tr('Sex'), tr('Sáb')];

  /// "1-5", "0,6", "5-1" → conjunto de dias (0 = domingo). Vazio = todo dia.
  static Set<int> parseDays(String text) {
    final out = <int>{};
    for (final part in text.split(',')) {
      final range = part.trim().split('-');
      final a = int.tryParse(range[0]);
      if (a == null || a < 0 || a > 6) continue;
      final b = range.length == 2 ? (int.tryParse(range[1]) ?? a).clamp(0, 6) : a;
      if (a <= b) {
        for (var d = a; d <= b; d++) {
          out.add(d);
        }
      } else {
        for (var d = a; d <= 6; d++) {
          out.add(d);
        }
        for (var d = 0; d <= b; d++) {
          out.add(d);
        }
      }
    }
    return out;
  }

  static String _daysText(String? days) {
    final set = parseDays(days ?? '');
    if (set.isEmpty || set.length == 7) return tr('todo dia');
    if (set.length == 5 && !set.contains(0) && !set.contains(6)) return tr('dias úteis');
    if (set.length == 2 && set.contains(0) && set.contains(6)) return tr('fim de semana');
    return (set.toList()..sort()).map((d) => dayNames[d]).join(', ');
  }

  String _describe(Map<String, dynamic> t) {
    final event = t['event'] as String? ?? 'schedule';
    final when = switch (event) {
      'schedule' => '${t['time'] ?? '--:--'} · ${_daysText(t['days'] as String?)}',
      'app_start' || 'app_exit' => '${events[event]}: ${t['app'] ?? ''}',
      'tally_on' || 'tally_off' => '${events[event]}: ${t['targetLabel'] ?? t['target'] ?? ''}',
      _ => events[event] ?? event,
    };
    return '$when → ${_buttonName(t)}';
  }

  Future<void> _edit([Map<String, dynamic>? existing]) async {
    final result = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (_) => _TriggerDialog(
        initial: existing ?? {'event': 'schedule', 'enabled': true},
        profiles: _profiles,
      ),
    );
    if (result == null) return;
    setState(() {
      if (existing != null) {
        _triggers[_triggers.indexOf(existing)] = result;
      } else {
        _triggers.add(result);
      }
    });
    await _save();
  }

  Future<void> _run(Map<String, dynamic> t) async {
    try {
      await _client.postJson('/api/triggers/${t['id']}/run');
      _snack(tr('Botão apertado.'));
    } catch (_) {
      _snack(tr('Salve a automação e confira se o botão ainda existe.'));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(tr('Automações'))),
      floatingActionButton: _loading || _error != null
          ? null
          : FloatingActionButton.extended(
              onPressed: () => _edit(),
              icon: const Icon(Icons.add),
              label: Text(tr('Nova automação')),
            ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!))
              : ListView(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                  children: [
                    Text(
                      tr('Uma automação aperta um botão do deck sozinha quando algo acontece '
                          'no PC. Crie antes o botão com a ação desejada.'),
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(height: 12),
                    if (_triggers.isEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 32),
                        child: Center(child: Text(tr('Nenhuma automação ainda.'))),
                      ),
                    for (final t in _triggers)
                      Card(
                        child: ListTile(
                          leading: Switch(
                            value: t['enabled'] != false,
                            onChanged: (v) {
                              setState(() => t['enabled'] = v);
                              _save();
                            },
                          ),
                          title: Text((t['name'] as String?)?.isNotEmpty == true
                              ? t['name'] as String
                              : events[t['event']] ?? ''),
                          subtitle: Text(_describe(t)),
                          onTap: () => _edit(t),
                          trailing: PopupMenuButton<String>(
                            onSelected: (v) async {
                              if (v == 'run') _run(t);
                              if (v == 'delete') {
                                setState(() => _triggers.remove(t));
                                await _save();
                              }
                            },
                            itemBuilder: (_) => [
                              PopupMenuItem(value: 'run', child: Text(tr('Testar agora'))),
                              PopupMenuItem(value: 'delete', child: Text(tr('Excluir'))),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
    );
  }
}

class _TriggerDialog extends StatefulWidget {
  final Map<String, dynamic> initial;
  final List<DeckProfile> profiles;

  const _TriggerDialog({required this.initial, required this.profiles});

  @override
  State<_TriggerDialog> createState() => _TriggerDialogState();
}

class _TriggerDialogState extends State<_TriggerDialog> {
  late final Map<String, dynamic> _t = Map.of(widget.initial);

  String get _event => _t['event'] as String? ?? 'schedule';

  /// "perfil|botão" de todos os botões com ação.
  Map<String, String> get _buttons => {
        for (final p in widget.profiles)
          for (final b in p.buttons.where((b) => b.action != null && b.action!.type != 'none'))
            '${p.id}|${b.id}':
                '${p.name} · ${b.label?.isNotEmpty == true ? b.label : tr('sem rótulo')}',
      };

  Widget _text(String key, String label, {String? hint, String? help}) => Padding(
        padding: const EdgeInsets.only(top: 12),
        child: TextFormField(
          initialValue: _t[key] as String? ?? '',
          decoration: InputDecoration(
            labelText: label,
            hintText: hint,
            helperText: help,
            helperMaxLines: 2,
            border: const OutlineInputBorder(),
          ),
          onChanged: (v) => _t[key] = v.trim(),
        ),
      );

  List<(String, String)> _apps = [];

  @override
  void initState() {
    super.initState();
    _loadApps();
  }

  /// Apps com janela aberta no PC agora, para escolher sem digitar o nome do processo.
  Future<void> _loadApps() async {
    try {
      final list = await Injector.get<DroidDeckClient>().getJson('/api/system/apps') as List;
      if (!mounted) return;
      setState(() => _apps = [
            for (final e in list) ('${(e as Map)['value']}', '${e['label'] ?? e['value']}'),
          ]);
    } catch (_) {}
  }

  Future<void> _pickTime() async {
    final parts = (_t['time'] as String? ?? '').split(':');
    final initial = parts.length == 2
        ? TimeOfDay(hour: int.tryParse(parts[0]) ?? 9, minute: int.tryParse(parts[1]) ?? 0)
        : const TimeOfDay(hour: 9, minute: 0);
    final picked = await showTimePicker(context: context, initialTime: initial);
    if (picked == null) return;
    String two(int v) => v.toString().padLeft(2, '0');
    setState(() => _t['time'] = '${two(picked.hour)}:${two(picked.minute)}');
  }

  Widget _timeField() {
    final time = _t['time'] as String?;
    return InkWell(
      onTap: _pickTime,
      child: InputDecorator(
        decoration: InputDecoration(
          labelText: tr('Horário'),
          border: const OutlineInputBorder(),
          suffixIcon: const Icon(Icons.schedule),
        ),
        child: Text(time?.isNotEmpty == true ? time! : tr('Escolher')),
      ),
    );
  }

  /// Dias da semana como botões; guarda no formato do PC ("1,2,3", 0 = domingo).
  Widget _daysPicker() {
    final names = _TriggersPageState.dayNames;
    final days = _TriggersPageState.parseDays(_t['days'] as String? ?? '');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(tr('Dias da semana'), style: Theme.of(context).textTheme.labelLarge),
        const SizedBox(height: 6),
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: [
            for (var d = 0; d < 7; d++)
              FilterChip(
                label: Text(names[d]),
                selected: days.isEmpty || days.contains(d),
                showCheckmark: false,
                onSelected: (on) => setState(() {
                  final next = days.isEmpty ? {0, 1, 2, 3, 4, 5, 6} : {...days};
                  on ? next.add(d) : next.remove(d);
                  // Todos marcados = todo dia (campo vazio).
                  _t['days'] = next.length == 7 || next.isEmpty
                      ? ''
                      : (next.toList()..sort()).join(',');
                }),
              ),
          ],
        ),
      ],
    );
  }

  /// App: escolhido entre os abertos agora, ou digitado (um app que ainda vai abrir).
  Widget _appPicker() {
    return Autocomplete<(String, String)>(
      initialValue: TextEditingValue(text: _t['app'] as String? ?? ''),
      displayStringForOption: (o) => o.$1,
      optionsBuilder: (v) {
        final q = v.text.toLowerCase();
        return _apps.where((a) =>
            q.isEmpty || a.$1.toLowerCase().contains(q) || a.$2.toLowerCase().contains(q));
      },
      onSelected: (o) => setState(() => _t['app'] = o.$1),
      optionsViewBuilder: (context, onSelected, options) => Align(
        alignment: Alignment.topLeft,
        child: Material(
          elevation: 4,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxHeight: 240, maxWidth: 400),
            child: ListView(
              shrinkWrap: true,
              children: [
                for (final o in options)
                  ListTile(
                    dense: true,
                    title: Text(o.$2, overflow: TextOverflow.ellipsis),
                    subtitle: Text(o.$1),
                    onTap: () => onSelected(o),
                  ),
              ],
            ),
          ),
        ),
      ),
      fieldViewBuilder: (context, controller, focus, onSubmit) => TextField(
        controller: controller,
        focusNode: focus,
        decoration: InputDecoration(
          labelText: tr('Aplicativo'),
          hintText: tr('Escolha um app aberto ou digite o nome'),
          helperText: tr('Abra o app no PC para ele aparecer na lista.'),
          border: const OutlineInputBorder(),
          suffixIcon: const Icon(Icons.apps),
        ),
        onChanged: (v) => setState(() => _t['app'] = v.trim()),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final buttons = _buttons;
    final selected = '${_t['profileId']}|${_t['buttonId']}';
    // Não deixa salvar uma automação que nunca dispararia.
    final valid = buttons.containsKey(selected) &&
        (_event != 'schedule' || (_t['time'] as String? ?? '').isNotEmpty) &&
        (!(_event == 'app_start' || _event == 'app_exit') ||
            (_t['app'] as String? ?? '').isNotEmpty) &&
        (!(_event == 'tally_on' || _event == 'tally_off') ||
            (_t['target'] as String? ?? '').isNotEmpty);
    return AlertDialog(
      title: Text(tr('Automação')),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _text('name', tr('Nome (opcional)')),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _event,
                isExpanded: true,
                decoration: InputDecoration(
                    labelText: tr('Quando'), border: const OutlineInputBorder()),
                items: [
                  for (final e in _TriggersPageState.events.entries)
                    DropdownMenuItem(value: e.key, child: Text(e.value)),
                ],
                onChanged: (v) => setState(() => _t['event'] = v ?? 'schedule'),
              ),
              if (_event == 'schedule') ...[
                const SizedBox(height: 12),
                _timeField(),
                const SizedBox(height: 12),
                _daysPicker(),
              ],
              if (_event == 'app_start' || _event == 'app_exit') ...[
                const SizedBox(height: 12),
                _appPicker(),
              ],
              if (_event == 'tally_on' || _event == 'tally_off') ...[
                const SizedBox(height: 12),
                TallyTargetField(
                  value: _t['target'] as String?,
                  onChanged: (id, label) => setState(() {
                    _t['target'] = id;
                    _t['targetLabel'] = label;
                  }),
                ),
              ],
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: valid ? selected : null,
                isExpanded: true,
                decoration: InputDecoration(
                    labelText: tr('Apertar o botão'), border: const OutlineInputBorder()),
                items: [
                  for (final e in buttons.entries)
                    DropdownMenuItem(
                        value: e.key, child: Text(e.value, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: (v) => setState(() {
                  final parts = (v ?? '|').split('|');
                  _t['profileId'] = parts[0];
                  _t['buttonId'] = parts[1];
                }),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.pop(context), child: Text(tr('Cancelar'))),
        FilledButton(
          onPressed: valid ? () => Navigator.pop(context, _t) : null,
          child: Text(tr('Salvar')),
        ),
      ],
    );
  }
}
