import 'dart:convert';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/stream_deck/actions/action_catalog.dart';
import 'package:droiddeck/src/stream_deck/actions/action_picker.dart';
import 'package:flutter/material.dart';

import '../buttons/colors.dart';
import 'editor/info_section.dart';
import 'editor/look_pickers.dart';
import 'editor/two_state_section.dart';

/// Editor de um botão: rótulo, ações (toque, toque longo, toque duplo), dois estados,
/// informação no botão, ícone e cores. A escolha de cada ação fica no [ActionPicker];
/// as seções maiores ficam em `editor/`.
class ButtonPropertyPanel extends StatefulWidget {
  final DeckButton button;
  final Function(DeckButton) onSave;
  final VoidCallback? onCancel;
  final List<DeckProfile> profiles;

  /// "Copiar ou mover…": leva o botão salvo para outro perfil. Nulo esconde a opção.
  final VoidCallback? onCopyOrMove;

  const ButtonPropertyPanel({
    super.key,
    required this.button,
    required this.onSave,
    this.onCancel,
    this.profiles = const [],
    this.onCopyOrMove,
  });

  @override
  State<ButtonPropertyPanel> createState() => ButtonPropertyPanelState();
}

class ButtonPropertyPanelState extends State<ButtonPropertyPanel> {
  final _catalog = ActionCatalog.instance;
  late final _labelController = TextEditingController(text: widget.button.label);

  /// Ação de cada gesto. Null = nenhuma.
  late DeckAction? _tap = widget.button.action?.type == 'none' ? null : widget.button.action;
  late DeckAction? _longPress = widget.button.longPressAction;
  late DeckAction? _doubleTap = widget.button.doubleTapAction;

  late Color _color = parseHexColor(widget.button.backgroundColor) ?? ColorRow.palette.first;
  late Color _activeColor = parseHexColor(widget.button.activeColor) ?? Colors.red[900]!;
  late String? _icon = widget.button.iconName;
  late String? _iconBase64 = widget.button.iconBase64;

  late String? _infoType = widget.button.dynamicType;
  late Map<String, String> _infoParams = Map.of(widget.button.dynamicParameters ?? const {});
  late TwoStateValue _twoState = TwoStateValue.of(widget.button);
  late int _columnSpan = widget.button.columnSpan;
  late int _rowSpan = widget.button.rowSpan;

  /// Ações que refletem um estado ligado/desligado (e portanto têm "cor de ativo").
  bool get _isStatefulAction => _catalog.find(_tap?.type)?.stateful ?? false;

  bool get _showsActive => _isStatefulAction || _twoState.toggle;

  /// No navegador o painel roda no configurador (localhost); fora dele, no celular.
  bool get _remote => !WebMode.configurator;

  @override
  void initState() {
    super.initState();
    _catalog.load();
    Future.delayed(const Duration(milliseconds: 400), () {
      if (mounted) _initialJson = jsonEncode(_compose().toJson());
    });
  }

  @override
  void dispose() {
    _labelController.dispose();
    super.dispose();
  }

  /// Id de um botão novo (fixo: o retrato de "não salvo" compara o botão inteiro).
  late final String _newId = DateTime.now().millisecondsSinceEpoch.toString();

  /// O botão como está no formulário agora.
  DeckButton _compose() {
    final hasInfo = _infoType != null && _infoType!.isNotEmpty;
    final activeLabel = _twoState.activeLabel.trim();
    return DeckButton(
      id: widget.button.id.isEmpty ? _newId : widget.button.id,
      row: widget.button.row,
      column: widget.button.column,
      label: _labelController.text,
      backgroundColor: colorToHex(_color),
      activeColor: _showsActive ? colorToHex(_activeColor) : null,
      iconName: _icon,
      iconBase64: _iconBase64,
      dynamicType: _infoType,
      dynamicParameters: hasInfo && _infoParams.isNotEmpty ? Map.of(_infoParams) : null,
      action: _tap,
      longPressAction: _longPress,
      doubleTapAction: _doubleTap,
      toggle: _twoState.toggle,
      activeAction: _twoState.toggle ? _twoState.activeAction : null,
      activeLabel: _showsActive && activeLabel.isNotEmpty ? activeLabel : null,
      activeIconName: _showsActive ? _twoState.activeIcon : null,
      activeIconBase64: _showsActive ? _twoState.activeIconBase64 : null,
      columnSpan: _columnSpan,
      rowSpan: _rowSpan,
    );
  }

  void _save() => widget.onSave(_compose());

  /// Retrato do formulário logo depois de abrir (os editores de ação ajustam os próprios
  /// parâmetros no primeiro frame; por isso a espera curta).
  String? _initialJson;

  /// Há mudanças não salvas?
  bool get isDirty =>
      _initialJson != null && jsonEncode(_compose().toJson()) != _initialJson;

  void _delete() {
    widget.onSave(DeckButton(
      id: '',
      row: widget.button.row,
      column: widget.button.column,
      label: '',
      backgroundColor: '',
    ));
  }

  /// Preenche o rótulo com a sugestão da ação, se ele estiver vazio ou no padrão.
  void _suggestLabel(String label) {
    final current = _labelController.text.trim();
    if (current.isEmpty ||
        current == tr('Novo botão') ||
        current == 'Novo botão' ||
        current == 'New Button') {
      _labelController.text = label;
    }
  }

  void _suggestIcon(String base64) => setState(() {
        _iconBase64 = base64;
        _icon = null;
      });

  Future<void> _pickImage() async {
    final String? image;
    try {
      image = await pickButtonImage();
    } on FormatException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
      return;
    }
    if (image != null) _suggestIcon(image);
  }

  ActionPicker _picker(String label, DeckAction? initial, ValueChanged<DeckAction?> onChanged) =>
      ActionPicker(
        label: label,
        initial: initial,
        onChanged: onChanged,
        remote: _remote,
        profiles: widget.profiles,
        suggestLabel: _suggestLabel,
        suggestIcon: _suggestIcon,
      );

  @override
  Widget build(BuildContext context) {
    // Watch: o catálogo do PC chega depois do primeiro build e pode mudar os campos.
    return Watch((context) {
      _catalog.schemas.value;
      return Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.button.id.isEmpty ? tr('Novo botão') : tr('Editar botão'),
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 24),
                  TextField(
                    controller: _labelController,
                    decoration: InputDecoration(
                      labelText: tr('Rótulo'),
                      border: const OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 16),
                  _picker(tr('Ação'), _tap, (a) => setState(() => _tap = a)),
                  const SizedBox(height: 16),
                  _buildGestures(),
                  const SizedBox(height: 12),
                  TwoStateSection(
                    value: _twoState,
                    statefulAction: _isStatefulAction,
                    actionPicker: _picker,
                    onChanged: (v) => setState(() => _twoState = v),
                  ),
                  const SizedBox(height: 12),
                  InfoSection(
                    type: _infoType,
                    params: _infoParams,
                    savedType: widget.button.dynamicType,
                    remote: _remote,
                    onChanged: (type, params) => setState(() {
                      _infoType = type;
                      _infoParams = params;
                    }),
                  ),
                  const SizedBox(height: 32),
                  ..._buildLook(),
                  const SizedBox(height: 24),
                ],
              ),
            ),
          ),
          _buildActionBar(),
        ],
      );
    });
  }

  /// Toque longo e toque duplo, recolhidos quando não usados.
  Widget _buildGestures() {
    final used = _longPress != null || _doubleTap != null;
    String title(DeckAction a) => _catalog.find(a.type)?.title ?? a.type;
    return Card(
      margin: EdgeInsets.zero,
      child: ExpansionTile(
        initiallyExpanded: used,
        shape: const Border(),
        leading: const Icon(Icons.touch_app_outlined),
        title: Text(tr('Toque longo e toque duplo')),
        subtitle: Text(
          used
              ? [
                  if (_longPress != null) tr('Longo: {action}', {'action': title(_longPress!)}),
                  if (_doubleTap != null) tr('Duplo: {action}', {'action': title(_doubleTap!)}),
                ].join(' · ')
              : tr('Opcional'),
          style: const TextStyle(fontSize: 12),
        ),
        childrenPadding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
        children: [
          Text(
            _remote
                ? tr('Com um toque longo definido, segure o botão para executá-lo. Para '
                    'editar o botão depois, use o lápis (modo de edição) na barra do deck.')
                : tr('Com um toque longo definido, segurar o botão no celular executa a '
                    'ação; o celular edita pelo modo de edição (lápis na barra do deck).'),
            style: TextStyle(fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
          ),
          const SizedBox(height: 12),
          _picker(tr('Toque longo'), _longPress, (a) => setState(() => _longPress = a)),
          const SizedBox(height: 20),
          _picker(tr('Toque duplo'), _doubleTap, (a) => setState(() => _doubleTap = a)),
        ],
      ),
    );
  }

  /// Ícone e cores.
  /// Tamanho do botão em casas da grade (largura × altura).
  Widget _buildSize() {
    Widget picker(String label, int value, ValueChanged<int> onChanged) => Expanded(
          child: DropdownButtonFormField<int>(
            initialValue: value,
            decoration: InputDecoration(
              labelText: label,
              border: const OutlineInputBorder(),
              isDense: true,
            ),
            items: [
              for (var n = 1; n <= 4; n++) DropdownMenuItem(value: n, child: Text('$n')),
            ],
            onChanged: (v) => setState(() => onChanged(v ?? 1)),
          ),
        );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(tr('Tamanho'), style: const TextStyle(fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        Row(
          children: [
            picker(tr('Largura (casas)'), _columnSpan, (v) => _columnSpan = v),
            const SizedBox(width: 12),
            picker(tr('Altura (casas)'), _rowSpan, (v) => _rowSpan = v),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          tr('Um botão grande não cobre outros botões: ele para onde houver um.'),
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
    );
  }

  List<Widget> _buildLook() {
    const bold = TextStyle(fontWeight: FontWeight.bold);
    return [
      _buildSize(),
      const SizedBox(height: 24),
      Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(tr('Ícone'), style: bold),
          TextButton.icon(
            onPressed: _pickImage,
            icon: const Icon(Icons.upload_file),
            label: Text(tr('Imagem própria')),
          ),
        ],
      ),
      const SizedBox(height: 8),
      if (_iconBase64 != null) ...[
        ImagePreview(base64: _iconBase64!, onRemove: () => setState(() => _iconBase64 = null)),
        const SizedBox(height: 8),
      ],
      IconGrid(
        selected: _icon,
        onSelect: (name) => setState(() {
          _icon = name;
          if (name != null) _iconBase64 = null;
        }),
      ),
      const SizedBox(height: 24),
      Text(tr('Cor de fundo'), style: bold),
      const SizedBox(height: 8),
      ColorRow(selected: _color, onSelect: (c) => setState(() => _color = c)),
      // Cor de "ativo": ações que refletem estado e botões de dois estados.
      if (_showsActive) ...[
        const SizedBox(height: 20),
        Text(
          switch (_tap?.type) {
            'discord' => tr('Cor quando ativo (mutado/ensurdecido)'),
            'timer' => tr('Cor quando a contagem termina'),
            'windows' => tr('Cor quando o dispositivo é o padrão'),
            _ => tr('Cor quando ativo'),
          },
          style: bold,
        ),
        const SizedBox(height: 8),
        ColorRow(selected: _activeColor, onSelect: (c) => setState(() => _activeColor = c)),
        const SizedBox(height: 12),
        ActivePreview(
          color: _color,
          activeColor: _activeColor,
          icons: ActivePreview.iconsFor(_tap),
        ),
      ],
    ];
  }

  Widget _buildActionBar() {
    return Container(
      padding: const EdgeInsets.all(16),
      color: Theme.of(context).cardColor,
      child: Row(
        children: [
          if (widget.button.id.isNotEmpty)
            TextButton(
              onPressed: _delete,
              style: TextButton.styleFrom(foregroundColor: Colors.red),
              child: Text(tr('Limpar botão')),
            ),
          // Copia o que está salvo: com mudanças pendentes, salve antes.
          if (widget.onCopyOrMove != null && widget.button.id.isNotEmpty)
            IconButton(
              tooltip: tr('Copiar ou mover…'),
              icon: const Icon(Icons.drive_file_move_outline),
              onPressed: widget.onCopyOrMove,
            ),
          const Spacer(),
          if (widget.onCancel != null)
            TextButton(onPressed: widget.onCancel, child: Text(tr('Cancelar'))),
          const SizedBox(width: 8),
          ElevatedButton(onPressed: _save, child: Text(tr('Salvar'))),
        ],
      ),
    );
  }
}
