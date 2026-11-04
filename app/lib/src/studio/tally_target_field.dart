import 'package:droiddeck/core/core.dart';
import 'package:flutter/material.dart';

/// Escolhe um alvo de tally entre o que está conectado agora. Um alvo salvo que não está na
/// lista (programa fechado) continua selecionável: salvar de novo não pode apagá-lo.
class TallyTargetField extends StatefulWidget {
  final String? value;

  /// Id do alvo e o nome que a lista mostra.
  final void Function(String id, String label) onChanged;

  const TallyTargetField({super.key, required this.value, required this.onChanged});

  @override
  State<TallyTargetField> createState() => _TallyTargetFieldState();
}

class _TallyTargetFieldState extends State<TallyTargetField> {
  List<(String, String)>? _options;
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
      setState(() => _options = [
            for (final e in list) ('${(e as Map)['value']}', '${e['label'] ?? e['value']}'),
          ]);
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final options = _options ?? const <(String, String)>[];
    final value = widget.value ?? '';
    final known = options.any((o) => o.$1 == value);
    return DropdownButtonFormField<String>(
      key: ValueKey('tally_${options.length}_$value'),
      initialValue: value.isEmpty ? null : value,
      isExpanded: true,
      isDense: true,
      decoration: InputDecoration(
        labelText: tr('O que acompanhar'),
        helperText: _failed
            ? tr('Não foi possível carregar a lista.')
            : tr('A lista traz só o que está conectado agora.'),
        helperMaxLines: 2,
        border: const OutlineInputBorder(),
      ),
      items: [
        for (final o in options)
          DropdownMenuItem(value: o.$1, child: Text(o.$2, overflow: TextOverflow.ellipsis)),
        if (value.isNotEmpty && !known)
          DropdownMenuItem(
            value: value,
            child: Text(tr('{value} (salvo)', {'value': value}), overflow: TextOverflow.ellipsis),
          ),
      ],
      onChanged: (v) {
        if (v == null) return;
        final label = options.where((o) => o.$1 == v).map((o) => o.$2).firstOrNull ?? v;
        widget.onChanged(v, label);
      },
    );
  }
}
