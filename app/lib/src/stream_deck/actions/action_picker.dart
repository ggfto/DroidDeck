import 'dart:convert';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/stream_deck/actions/action_catalog.dart';
import 'package:droiddeck/src/stream_deck/actions/action_schema.dart';
import 'package:droiddeck/src/stream_deck/actions/editors/action_editor.dart';
import 'package:droiddeck/src/stream_deck/actions/schema_action_form.dart';
import 'package:flutter/material.dart';

/// Escolha de uma ação: o tipo (agrupado por categoria) e os campos dele. Um botão tem
/// uma destas para o toque e, opcionalmente, para o toque longo e o toque duplo.
///
/// Os campos vêm do catálogo do PC (GET /api/actions/schema): tipos simples viram um
/// formulário gerado ([SchemaActionForm]); os que precisam de tela própria (Discord,
/// Tuya, soundboard...) ficam em actions/editors/.
class ActionPicker extends StatefulWidget {
  final DeckAction? initial;

  /// Nova ação a cada mudança; null = "Nenhuma".
  final ValueChanged<DeckAction?> onChanged;

  /// Editando de fora do PC (celular): sem tipos que executam código.
  final bool remote;
  final List<DeckProfile> profiles;
  final ValueChanged<String> suggestLabel;
  final ValueChanged<String> suggestIcon;
  /// Rótulo do seletor; null = "Ação".
  final String? label;

  const ActionPicker({
    super.key,
    required this.initial,
    required this.onChanged,
    required this.remote,
    required this.suggestLabel,
    required this.suggestIcon,
    this.profiles = const [],
    this.label,
  });

  /// Abrir app, atalho, ativar janela e HTTP executam código (ou acessam a rede) a
  /// partir do PC. O backend só aceita criá-los ou alterá-los pelo configurador em
  /// localhost (ActionPolicy): no celular o seletor não oferece esses tipos e trava a
  /// ação dos botões que já os têm — o botão continua funcionando.
  static const privilegedTypes = {
    'hotkey',
    'key',
    'keyboard_key',
    'send_key',
    'launch_app',
    'launchApp',
    'activatewindow',
    'http',
    'script',
  };

  static bool isPrivilegedType(String type) =>
      privilegedTypes.contains(type) ||
      (ActionCatalog.instance.find(type)?.privileged ?? false);

  static bool isPrivileged(DeckAction? action) {
    if (action == null) return false;
    if (isPrivilegedType(action.type)) return true;
    if (action.type != 'multi') return false;
    try {
      final steps = jsonDecode(action.parameters['steps'] ?? '[]') as List;
      return steps.any((s) => isPrivilegedType('${(s as Map)['type']}'));
    } catch (_) {
      return true;
    }
  }

  @override
  State<ActionPicker> createState() => _ActionPickerState();
}

class _ActionPickerState extends State<ActionPicker> {
  final _catalog = ActionCatalog.instance;
  late String _type;
  late Map<String, String> _params;
  late final bool _locked =
      widget.remote && ActionPicker.isPrivileged(widget.initial);

  @override
  void initState() {
    super.initState();
    final a = widget.initial;
    _type = a == null || a.type == 'none'
        ? 'none'
        : (a.type == 'launchApp' ? 'launch_app' : a.type);
    _params = Map<String, String>.from(a?.parameters ?? const {});
  }

  void _emit() {
    if (_locked) return; // a ação travada segue como está
    widget.onChanged(
        _type == 'none' ? null : DeckAction(type: _type, parameters: Map.of(_params)));
  }

  void _selectType(String type) {
    setState(() {
      if (type != _type) _params = {};
      _type = type;
    });
    _emit();
  }

  void _setParams(Map<String, String> p) {
    setState(() => _params = p);
    _emit();
  }

  List<ActionSchema> get _available => _catalog.schemas.value
      .where((s) => !widget.remote || _locked || !s.privileged)
      .toList();

  @override
  Widget build(BuildContext context) {
    return Watch((context) {
      _catalog.schemas.value;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (_locked) ...[
            _lockedNotice(),
            const SizedBox(height: 16),
          ],
          _typePicker(context),
          const SizedBox(height: 16),
          _editor(),
        ],
      );
    });
  }

  /// Campo do tipo de ação: mostra o escolhido e abre o seletor com busca.
  Widget _typePicker(BuildContext context) {
    final schema = _catalog.find(_type);
    final scheme = Theme.of(context).colorScheme;
    final title = _type == 'none'
        ? tr('Nenhuma')
        : (schema?.title ?? tr('{type} (desconhecido)', {'type': _type}));
    return Semantics(
      button: true,
      label: '${widget.label ?? tr('Ação')}: $title',
      child: InkWell(
        borderRadius: BorderRadius.circular(4),
        onTap: _locked ? null : () => _openTypeSheet(context),
        child: InputDecorator(
          decoration: InputDecoration(
            labelText: widget.label ?? tr('Ação'),
            border: const OutlineInputBorder(),
            enabled: !_locked,
            suffixIcon: const Icon(Icons.arrow_drop_down),
          ),
          child: Row(
            children: [
              Icon(schema?.iconData ?? Icons.block, size: 20,
                  color: _type == 'none' ? scheme.onSurfaceVariant : scheme.primary),
              const SizedBox(width: 10),
              Expanded(child: Text(title, overflow: TextOverflow.ellipsis)),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _openTypeSheet(BuildContext context) async {
    final picked = await showDialog<String>(
      context: context,
      builder: (_) => _ActionTypeDialog(schemas: _available, current: _type),
    );
    if (picked != null) _selectType(picked);
  }

  /// Campos do tipo escolhido: tela própria ou formulário gerado pelo schema.
  Widget _editor() {
    final schema = _catalog.find(_type);
    if (_locked || schema == null) return const SizedBox.shrink();

    // A chave pelo tipo recria o editor (e o estado dele) ao trocar de tipo.
    final key = ValueKey('editor_${schema.type}');
    final args = ActionEditorArgs(
      initial: _params,
      onChanged: _setParams,
      suggestLabel: widget.suggestLabel,
      suggestIcon: widget.suggestIcon,
      remote: widget.remote,
      profiles: widget.profiles,
    );

    final custom = schema.editor != null
        ? buildCustomActionEditor(schema.editor!, args)
        : null;
    if (custom != null) return KeyedSubtree(key: key, child: custom);

    if (schema.fields.isEmpty) {
      return schema.description != null
          ? EditorHint(schema.description!)
          : const SizedBox.shrink();
    }
    return SchemaActionForm(
      key: key,
      schema: schema,
      initial: _params,
      onChanged: _setParams,
    );
  }

  /// Aviso no lugar dos campos de uma ação que só pode mudar no PC.
  Widget _lockedNotice() {
    final a = widget.initial!;
    final String summary;
    switch (a.type) {
      case 'hotkey':
        summary = tr('Atalho: {keys}', {'keys': a.parameters['keys'] ?? ''});
      case 'launch_app':
      case 'launchApp':
        summary = tr('Abre: {path}', {'path': a.parameters['path'] ?? ''});
      case 'http':
        summary = tr('Requisição: {url}', {'url': a.parameters['url'] ?? ''});
      case 'multi':
        summary = tr('Multi-ação');
      default:
        summary = _catalog.find(a.type)?.title ?? a.type;
    }
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.amber.withValues(alpha: 0.12),
        border: Border.all(color: Colors.amber.withValues(alpha: 0.5)),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.lock_outline, color: Colors.amber),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(summary,
                    style: const TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 4),
                Text(
                  tr('Ações que abrem programas, enviam atalhos, rodam scripts ou fazem '
                      'requisições só mudam no editor do PC: clique no ícone do DroidDeck '
                      'na bandeja → "Abrir no navegador". Aqui você pode mudar o nome, o '
                      'ícone e as cores.'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}


/// Escolher o tipo de ação: busca, as mais usadas no topo e o resto por categoria, cada
/// uma com uma linha dizendo o que faz.
class _ActionTypeDialog extends StatefulWidget {
  final List<ActionSchema> schemas;
  final String current;

  const _ActionTypeDialog({required this.schemas, required this.current});

  /// O que a maioria procura primeiro.
  static const popular = ['hotkey', 'mixer', 'media', 'launch_app', 'obs'];

  @override
  State<_ActionTypeDialog> createState() => _ActionTypeDialogState();
}

class _ActionTypeDialogState extends State<_ActionTypeDialog> {
  String _query = '';

  static String _fold(String s) => s
      .toLowerCase()
      .replaceAll(RegExp('[áàâã]'), 'a')
      .replaceAll(RegExp('[éê]'), 'e')
      .replaceAll('í', 'i')
      .replaceAll(RegExp('[óôõ]'), 'o')
      .replaceAll('ú', 'u')
      .replaceAll('ç', 'c');

  bool _matches(ActionSchema s) {
    if (_query.isEmpty) return true;
    final q = _fold(_query);
    return _fold(s.title).contains(q) ||
        _fold(s.category).contains(q) ||
        _fold(s.description ?? '').contains(q) ||
        s.type.contains(q);
  }

  Widget _tile(ActionSchema s) {
    final selected = s.type == widget.current;
    return ListTile(
      selected: selected,
      leading: Icon(s.iconData),
      title: Text(s.title),
      subtitle: s.description == null
          ? null
          : Text(s.description!, maxLines: 2, overflow: TextOverflow.ellipsis),
      trailing: selected ? const Icon(Icons.check) : null,
      onTap: () => Navigator.pop(context, s.type),
    );
  }

  Widget _header(String text) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
        child: Text(text,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
                fontWeight: FontWeight.w600)),
      );

  @override
  Widget build(BuildContext context) {
    final found = widget.schemas.where(_matches).toList();
    final categories = <String, List<ActionSchema>>{};
    for (final s in found) {
      categories.putIfAbsent(s.category, () => []).add(s);
    }
    final popular = _query.isEmpty
        ? [
            for (final t in _ActionTypeDialog.popular)
              ...widget.schemas.where((s) => s.type == t),
          ]
        : const <ActionSchema>[];

    return Dialog(
      insetPadding: const EdgeInsets.all(16),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520, maxHeight: 640),
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: TextField(
                autofocus: true,
                decoration: InputDecoration(
                  prefixIcon: const Icon(Icons.search),
                  hintText: tr('Buscar ação (ex.: mudo, OBS, atalho)'),
                  border: const OutlineInputBorder(),
                  isDense: true,
                ),
                onChanged: (v) => setState(() => _query = v.trim()),
              ),
            ),
            Expanded(
              child: ListView(
                children: [
                  if (_query.isEmpty)
                    ListTile(
                      leading: const Icon(Icons.block),
                      title: Text(tr('Nenhuma')),
                      subtitle: Text(tr('O botão só mostra o rótulo ou uma informação.')),
                      selected: widget.current == 'none',
                      onTap: () => Navigator.pop(context, 'none'),
                    ),
                  if (popular.isNotEmpty) ...[
                    _header(tr('Mais usadas')),
                    for (final s in popular) _tile(s),
                  ],
                  for (final entry in categories.entries) ...[
                    _header(entry.key),
                    for (final s in entry.value) _tile(s),
                  ],
                  if (found.isEmpty)
                    Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(tr('Nenhuma ação com "{q}".', {'q': _query}),
                          textAlign: TextAlign.center),
                    ),
                ],
              ),
            ),
            Align(
              alignment: Alignment.centerRight,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
                child: TextButton(
                    onPressed: () => Navigator.pop(context), child: Text(tr('Cancelar'))),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
