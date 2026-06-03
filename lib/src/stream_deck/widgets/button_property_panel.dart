import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

class ButtonPropertyPanel extends StatefulWidget {
  final DeckButton button;
  final Function(DeckButton) onSave;
  final VoidCallback? onCancel;
  final List<DeckProfile> profiles;

  const ButtonPropertyPanel({
    super.key,
    required this.button,
    required this.onSave,
    this.onCancel,
    this.profiles = const [],
  });

  @override
  State<ButtonPropertyPanel> createState() => _ButtonPropertyPanelState();
}

class _ButtonPropertyPanelState extends State<ButtonPropertyPanel> {
  late TextEditingController _labelController;
  late TextEditingController _actionParamController;
  String _selectedActionType = 'none';
  String _mixerOperation = 'toggleMute';
  String _discordOp = 'toggleMute';
  // Discord avançado (canal de voz / usuário / volume)
  List<Map<String, String>> _dcGuilds = [];
  List<Map<String, String>> _dcChannels = [];
  List<Map<String, dynamic>> _dcParticipants = [];
  String? _dcGuildId;
  String? _dcChannelId;
  String? _dcChannelName;
  String? _dcUserId;
  String? _dcUserName;
  int _dcDelta = 10;
  int _dcValue = 100;
  String? _targetProfileId;
  // Passos da multi-ação: cada item = {type, param, delayMs, _k(chave estável)}.
  List<Map<String, dynamic>> _multiSteps = [];
  int _stepSeq = 0;
  Color _selectedColor = Colors.grey[800]!;
  Color _activeColor = Colors.red[900]!;

  /// Ações que refletem um estado ligado/desligado (e portanto têm "cor de ativo").
  bool get _isStatefulAction =>
      _selectedActionType == 'discord' || _selectedActionType == 'mixer';

  final List<String> _actionTypes = [
    'none',
    'hotkey',
    'launch_app',
    'mixer',
    'open_profile',
    'back',
    'multi',
    'discord',
  ];
  final List<Color> _colors = [
    Colors.grey[800]!,
    Colors.red[900]!,
    Colors.blue[900]!,
    Colors.green[900]!,
    Colors.orange[900]!,
    Colors.purple[900]!,
    Colors.black,
  ];

  String? _selectedIcon;
  String? _iconBase64;
  String? _dynamicType;

  // TODO: Centralize icon list
  final List<String> _icons = [
    'play',
    'pause',
    'volume_up',
    'volume_off',
    'mic',
    'mic_off',
    'keyboard',
    'launch',
    'home',
    'settings',
    'code',
    'videocam',
    'call_end',
    'folder',
    'save',
    'delete'
  ];

  @override
  void initState() {
    super.initState();
    _labelController = TextEditingController(text: widget.button.label);
    _selectedIcon = widget.button.iconName;

    // Initialize Action State
    if (widget.button.action != null) {
      _selectedActionType = widget.button.action!.type;
      if (_selectedActionType == 'hotkey') {
        _actionParamController = TextEditingController(
            text: widget.button.action!.parameters['keys'] ?? '');
      } else if (_selectedActionType == 'launch_app') {
        _actionParamController = TextEditingController(
            text: widget.button.action!.parameters['path'] ?? '');
      } else if (_selectedActionType == 'mixer') {
        _actionParamController = TextEditingController(
            text: widget.button.action!.parameters['processName'] ?? '');
        _mixerOperation =
            widget.button.action!.parameters['operation'] ?? 'toggleMute';
      } else if (_selectedActionType == 'open_profile') {
        _actionParamController = TextEditingController();
        _targetProfileId = widget.button.action!.parameters['profileId'];
      } else if (_selectedActionType == 'multi') {
        _actionParamController = TextEditingController();
        _multiSteps = _decodeSteps(widget.button.action!.parameters['steps']);
      } else if (_selectedActionType == 'discord') {
        _actionParamController = TextEditingController();
        final dp = widget.button.action!.parameters;
        _discordOp = dp['operation'] ?? 'toggleMute';
        _dcChannelId = dp['channelId'];
        _dcChannelName = dp['channelName'];
        _dcUserId = dp['userId'];
        _dcUserName = dp['userName'];
        _dcDelta = int.tryParse(dp['delta'] ?? '') ?? 10;
        _dcValue = int.tryParse(dp['value'] ?? '') ?? 100;
        _loadDiscordDataFor(_discordOp);
      } else {
        _actionParamController = TextEditingController();
      }
    } else {
      _actionParamController = TextEditingController();
    }

    // Initialize Color State
    if (widget.button.backgroundColor != null &&
        widget.button.backgroundColor!.isNotEmpty) {
      _selectedColor =
          _parseColor(widget.button.backgroundColor!) ?? Colors.grey[800]!;
    }
    if (widget.button.activeColor != null &&
        widget.button.activeColor!.isNotEmpty) {
      _activeColor =
          _parseColor(widget.button.activeColor!) ?? Colors.red[900]!;
    }
    _iconBase64 = widget.button.iconBase64;
    _dynamicType = widget.button.dynamicType;
  }

  Future<void> _pickImage() async {
    final ImagePicker picker = ImagePicker();
    final XFile? image = await picker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 512,
      maxHeight: 512,
    );

    if (image != null) {
      final bytes = await image.readAsBytes();
      final base64 = base64Encode(bytes);
      setState(() {
        _iconBase64 = base64;
        _selectedIcon = null; // Clear standard icon
      });
    }
  }

  Color? _parseColor(String? hexString) {
    if (hexString == null || hexString.isEmpty) return null;
    try {
      final buffer = StringBuffer();
      if (hexString.length == 6 || hexString.length == 7) buffer.write('ff');
      buffer.write(hexString.replaceFirst('#', ''));
      return Color(int.parse(buffer.toString(), radix: 16));
    } catch (_) {
      return null;
    }
  }

  String _colorToHex(Color color) {
    return '#${color.value.toRadixString(16).substring(2).toUpperCase()}';
  }

  // ---- Multi-ação: codec entre os passos do editor e o JSON do backend ----
  // Editor step: {'type': hotkey|launch_app|mute, 'param': String, 'delayMs': int}
  List<Map<String, dynamic>> _decodeSteps(String? json) {
    if (json == null || json.isEmpty) return [];
    try {
      final list = jsonDecode(json) as List;
      return list.map<Map<String, dynamic>>((e) {
        final m = e as Map<String, dynamic>;
        final t = m['type'] as String? ?? 'hotkey';
        final p = (m['parameters'] as Map?)?.cast<String, dynamic>() ?? {};
        String editorType = t;
        String param = '';
        if (t == 'hotkey') {
          param = p['keys']?.toString() ?? '';
        } else if (t == 'launch_app') {
          param = p['path']?.toString() ?? '';
        } else if (t == 'mixer') {
          editorType = 'mute';
          param = p['processName']?.toString() ?? '';
        }
        return {
          'type': editorType,
          'param': param,
          'delayMs': (m['delayMs'] as num?)?.toInt() ?? 0,
          '_k': _stepSeq++,
        };
      }).toList();
    } catch (_) {
      return [];
    }
  }

  String _encodeSteps(List<Map<String, dynamic>> steps) {
    final out = steps.map((s) {
      final type = s['type'] as String? ?? 'hotkey';
      final param = (s['param'] as String? ?? '').trim();
      final delayMs = (s['delayMs'] as int?) ?? 0;
      if (type == 'mute') {
        return {
          'type': 'mixer',
          'parameters': {'operation': 'toggleMute', 'processName': param},
          'delayMs': delayMs,
        };
      } else if (type == 'launch_app') {
        return {
          'type': 'launch_app',
          'parameters': {'path': param},
          'delayMs': delayMs,
        };
      }
      return {
        'type': 'hotkey',
        'parameters': {'keys': param},
        'delayMs': delayMs,
      };
    }).toList();
    return jsonEncode(out);
  }

  String _stepParamLabel(String type) {
    switch (type) {
      case 'launch_app':
        return 'Caminho do app (.exe)';
      case 'mute':
        return 'Nome do app (processo)';
      default:
        return 'Teclas (ex.: ^C = Ctrl+C)';
    }
  }

  @override
  void dispose() {
    _labelController.dispose();
    _actionParamController.dispose();
    super.dispose();
  }

  void _save() {
    Map<String, String> params = {};
    if (_selectedActionType == 'hotkey') {
      params['keys'] = _actionParamController.text;
    } else if (_selectedActionType == 'launch_app') {
      params['path'] = _actionParamController.text;
    } else if (_selectedActionType == 'mixer') {
      params['operation'] = _mixerOperation;
      params['processName'] = _actionParamController.text.trim();
    } else if (_selectedActionType == 'open_profile') {
      if (_targetProfileId != null) params['profileId'] = _targetProfileId!;
    } else if (_selectedActionType == 'multi') {
      params['steps'] = _encodeSteps(_multiSteps);
    } else if (_selectedActionType == 'discord') {
      params['operation'] = _discordOp;
      if (_discordOp == 'joinChannel') {
        if (_dcChannelId != null) params['channelId'] = _dcChannelId!;
        if (_dcChannelName != null) params['channelName'] = _dcChannelName!;
      } else if (_discordOp == 'userMute' || _discordOp == 'userVolume') {
        if (_dcUserId != null) params['userId'] = _dcUserId!;
        if (_dcUserName != null) params['userName'] = _dcUserName!;
        if (_discordOp == 'userVolume') params['value'] = '$_dcValue';
      } else if (_discordOp.toLowerCase().contains('volume')) {
        params['delta'] = '$_dcDelta';
      }
    }
    // 'back' não precisa de parâmetros.

    // Monitor (dynamicType) e ação são exclusivos: botão-monitor não guarda ação.
    final isDynamic = _dynamicType != null && _dynamicType!.isNotEmpty;
    final newAction = (isDynamic || _selectedActionType == 'none')
        ? null
        : DeckAction(type: _selectedActionType, parameters: params);

    final newButton = DeckButton(
      id: widget.button.id.isEmpty
          ? DateTime.now().millisecondsSinceEpoch.toString()
          : widget.button.id,
      row: widget.button.row,
      column: widget.button.column,
      label: _labelController.text,
      backgroundColor: _colorToHex(_selectedColor),
      activeColor: _isStatefulAction ? _colorToHex(_activeColor) : null,
      iconName: _selectedIcon,
      iconBase64: _iconBase64,
      dynamicType: _dynamicType,
      action: newAction,
    );

    widget.onSave(newButton);
  }

  void _delete() {
    final clearedButton = DeckButton(
      id: '',
      row: widget.button.row,
      column: widget.button.column,
      label: '',
      backgroundColor: '',
      action: null,
      iconName: null,
      iconBase64: null,
    );
    widget.onSave(clearedButton);
  }

  @override
  Widget build(BuildContext context) {
    // For panel usage, we might want a scroll view
    return Column(
      children: [
        Expanded(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  widget.button.id.isEmpty ? 'New Button' : 'Edit Button',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 24),

                // Label
                TextField(
                  controller: _labelController,
                  decoration: const InputDecoration(
                    labelText: 'Label',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),

                // Action Type
                DropdownButtonFormField<String>(
                  value: _actionTypes.contains(_selectedActionType)
                      ? _selectedActionType
                      : 'none',
                  decoration: const InputDecoration(
                    labelText: 'Action Type',
                    border: OutlineInputBorder(),
                  ),
                  items: _actionTypes.map((type) {
                    return DropdownMenuItem(
                      value: type,
                      child: Text(type.toUpperCase().replaceAll('_', ' ')),
                    );
                  }).toList(),
                  onChanged: (val) {
                    setState(() {
                      _selectedActionType = val!;
                      // Ação e monitor (dynamicType) são exclusivos.
                      if (val != 'none') _dynamicType = null;
                    });
                  },
                ),
                const SizedBox(height: 16),

                // Dynamic Parameters
                if (_selectedActionType == 'hotkey') ...[
                  TextField(
                    controller: _actionParamController,
                    decoration: const InputDecoration(
                      labelText: 'Keys (e.g. ^C for Ctrl+C)',
                      helperText: '^=Ctrl, +=Shift, %=Alt',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ],
                if (_selectedActionType == 'launch_app') ...[
                  TextField(
                    controller: _actionParamController,
                    decoration: const InputDecoration(
                      labelText: 'Application Path (.exe)',
                      hintText: 'C:\\Windows\\System32\\notepad.exe',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ],
                if (_selectedActionType == 'open_profile') ...[
                  DropdownButtonFormField<String>(
                    value: widget.profiles.any((p) => p.id == _targetProfileId)
                        ? _targetProfileId
                        : null,
                    decoration: const InputDecoration(
                      labelText: 'Abrir pasta/perfil',
                      border: OutlineInputBorder(),
                    ),
                    items: widget.profiles
                        .map((p) => DropdownMenuItem(
                            value: p.id, child: Text(p.name)))
                        .toList(),
                    onChanged: (val) => setState(() => _targetProfileId = val),
                  ),
                  const Padding(
                    padding: EdgeInsets.only(top: 6),
                    child: Text(
                      'Ao tocar, abre esse perfil como uma pasta (use um botão BACK para voltar).',
                      style: TextStyle(fontSize: 11, color: Colors.grey),
                    ),
                  ),
                ],
                if (_selectedActionType == 'back') ...[
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                      'Botão de voltar: retorna para a pasta/perfil anterior.',
                      style: TextStyle(color: Colors.grey),
                    ),
                  ),
                ],
                if (_selectedActionType == 'multi') ...[
                  const Text('Passos (executados em sequência):',
                      style: TextStyle(fontWeight: FontWeight.bold)),
                  const SizedBox(height: 8),
                  ..._multiSteps.map((step) {
                    final k = step['_k'];
                    return Card(
                      margin: const EdgeInsets.only(bottom: 8),
                      child: Padding(
                        padding: const EdgeInsets.all(8),
                        child: Column(
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: DropdownButtonFormField<String>(
                                    value: step['type'] as String,
                                    isDense: true,
                                    decoration: const InputDecoration(
                                        labelText: 'Tipo',
                                        border: OutlineInputBorder()),
                                    items: const [
                                      DropdownMenuItem(
                                          value: 'hotkey', child: Text('Hotkey')),
                                      DropdownMenuItem(
                                          value: 'launch_app',
                                          child: Text('Abrir app')),
                                      DropdownMenuItem(
                                          value: 'mute',
                                          child: Text('Mutar app')),
                                    ],
                                    onChanged: (v) => setState(
                                        () => step['type'] = v ?? 'hotkey'),
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline,
                                      color: Colors.redAccent),
                                  onPressed: () =>
                                      setState(() => _multiSteps.remove(step)),
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            TextFormField(
                              key: ValueKey('p$k'),
                              initialValue: step['param'] as String? ?? '',
                              decoration: InputDecoration(
                                labelText:
                                    _stepParamLabel(step['type'] as String),
                                isDense: true,
                                border: const OutlineInputBorder(),
                              ),
                              onChanged: (v) => step['param'] = v,
                            ),
                            const SizedBox(height: 8),
                            TextFormField(
                              key: ValueKey('d$k'),
                              initialValue: (step['delayMs'] as int).toString(),
                              keyboardType: TextInputType.number,
                              decoration: const InputDecoration(
                                labelText: 'Esperar antes (ms)',
                                isDense: true,
                                border: OutlineInputBorder(),
                              ),
                              onChanged: (v) =>
                                  step['delayMs'] = int.tryParse(v) ?? 0,
                            ),
                          ],
                        ),
                      ),
                    );
                  }),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: () => setState(() => _multiSteps.add({
                            'type': 'hotkey',
                            'param': '',
                            'delayMs': 0,
                            '_k': _stepSeq++,
                          })),
                      icon: const Icon(Icons.add),
                      label: const Text('Adicionar passo'),
                    ),
                  ),
                ],
                if (_selectedActionType == 'discord') ...[
                  ..._buildDiscordFields(),
                ],
                if (_selectedActionType == 'mixer') ...[
                  DropdownButtonFormField<String>(
                    value: _mixerOperation,
                    decoration: const InputDecoration(
                      labelText: 'Operação',
                      border: OutlineInputBorder(),
                    ),
                    items: const [
                      DropdownMenuItem(
                          value: 'toggleMute', child: Text('Alternar mudo')),
                      DropdownMenuItem(value: 'mute', child: Text('Mutar')),
                      DropdownMenuItem(value: 'unmute', child: Text('Desmutar')),
                    ],
                    onChanged: (val) =>
                        setState(() => _mixerOperation = val ?? 'toggleMute'),
                  ),
                  const SizedBox(height: 16),
                  TextField(
                    controller: _actionParamController,
                    decoration: const InputDecoration(
                      labelText: 'Nome do app (processo)',
                      hintText: 'Spotify, Discord, chrome…',
                      helperText: 'Sem .exe — o nome do processo no Windows',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ],

                const SizedBox(height: 24),

                const SizedBox(height: 24),

                // Icon Picker
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text('Icon',
                        style: TextStyle(fontWeight: FontWeight.bold)),
                    TextButton.icon(
                      onPressed: _pickImage,
                      icon: const Icon(Icons.upload_file),
                      label: const Text('Custom Image'),
                    ),
                  ],
                ),
                const SizedBox(height: 8),

                if (_iconBase64 != null) ...[
                  Center(
                    child: Column(
                      children: [
                        Container(
                          width: 80,
                          height: 80,
                          decoration: BoxDecoration(
                            border: Border.all(color: Colors.blueAccent),
                            borderRadius: BorderRadius.circular(8),
                            color: Colors.black12,
                          ),
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(8),
                            child: Image.memory(
                              base64Decode(_iconBase64!),
                              fit: BoxFit.contain,
                              errorBuilder: (_, __, ___) =>
                                  const Icon(Icons.error, color: Colors.red),
                            ),
                          ),
                        ),
                        TextButton(
                          onPressed: () => setState(() => _iconBase64 = null),
                          style: TextButton.styleFrom(
                              foregroundColor: Colors.redAccent),
                          child: const Text("Remove Image"),
                        )
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                ],

                Wrap(
                  spacing: 12,
                  runSpacing: 12,
                  children: [
                    _buildIconOption(null), // Clear icon
                    ..._icons.map((icon) => _buildIconOption(icon)).toList(),
                  ],
                ),

                const SizedBox(height: 24),

                // Color Config
                const Text('Background Color',
                    style: TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: _colors
                      .map((c) => InkWell(
                            onTap: () {
                              setState(() {
                                _selectedColor = c;
                              });
                            },
                            child: Container(
                              width: 32,
                              height: 32,
                              decoration: BoxDecoration(
                                  color: c,
                                  shape: BoxShape.circle,
                                  border: _selectedColor == c
                                      ? Border.all(
                                          color: Colors.white, width: 2)
                                      : null,
                                  boxShadow: [
                                    BoxShadow(
                                        blurRadius: 2, color: Colors.black26)
                                  ]),
                            ),
                          ))
                      .toList(),
                ),

                // Cor de "ativo" (toggle): só para ações que refletem estado.
                if (_isStatefulAction) ...[
                  const SizedBox(height: 20),
                  Text(
                    _selectedActionType == 'discord'
                        ? 'Cor quando ativo (mutado/ensurdecido)'
                        : 'Cor quando ativo (mutado)',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: _colors
                        .map((c) => InkWell(
                              onTap: () => setState(() => _activeColor = c),
                              child: Container(
                                width: 32,
                                height: 32,
                                decoration: BoxDecoration(
                                    color: c,
                                    shape: BoxShape.circle,
                                    border: _activeColor == c
                                        ? Border.all(
                                            color: Colors.white, width: 2)
                                        : null,
                                    boxShadow: const [
                                      BoxShadow(
                                          blurRadius: 2, color: Colors.black26)
                                    ]),
                              ),
                            ))
                        .toList(),
                  ),
                  const SizedBox(height: 12),
                  _buildTogglePreview(),
                ],

                // Dynamic Type
                const SizedBox(height: 16),
                DropdownButtonFormField<String>(
                  value: _dynamicType,
                  decoration: const InputDecoration(
                    labelText: 'Dynamic Feature',
                    border: OutlineInputBorder(),
                  ),
                  items: [
                    DropdownMenuItem(value: null, child: Text('None')),
                    DropdownMenuItem(
                        value: 'cpu_monitor', child: Text('CPU Monitor')),
                    DropdownMenuItem(
                        value: 'memory_monitor', child: Text('Memory Monitor')),
                    DropdownMenuItem(
                        value: 'gpu_monitor', child: Text('GPU Monitor')),
                    DropdownMenuItem(
                        value: 'network_monitor', child: Text('Network Monitor')),
                  ],
                  onChanged: (val) {
                    setState(() {
                      _dynamicType = val;
                      // Monitor é só display: zera a ação (eram exclusivos).
                      if (val != null && val.isNotEmpty) {
                        _selectedActionType = 'none';
                      }
                    });
                  },
                ),

                const SizedBox(height: 24),
              ],
            ),
          ),
        ),
        // Action Bar
        Container(
          padding: const EdgeInsets.all(16),
          color: Theme.of(context).cardColor,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              if (widget.button.id.isNotEmpty)
                TextButton(
                  onPressed: _delete,
                  style: TextButton.styleFrom(foregroundColor: Colors.red),
                  child: const Text('Clear Button'),
                ),
              const Spacer(),
              if (widget.onCancel != null)
                TextButton(
                  onPressed: widget.onCancel,
                  child: const Text('Cancel'),
                ),
              const SizedBox(width: 8),
              ElevatedButton(
                onPressed: _save,
                child: const Text('Save Changes'),
              ),
            ],
          ),
        ),
      ],
    );
  }

  // ---- Discord: carregamento de servidores/canais/participantes ----
  void _loadDiscordDataFor(String op) {
    if (op == 'joinChannel' && _dcGuilds.isEmpty) _loadGuilds();
    if ((op == 'userMute' || op == 'userVolume') && _dcParticipants.isEmpty) {
      _loadParticipants();
    }
  }

  Future<void> _loadGuilds() async {
    final g = await Injector.get<AnyDeckClient>().getDiscordGuilds();
    if (mounted) setState(() => _dcGuilds = g);
  }

  Future<void> _loadChannels(String guildId) async {
    final c = await Injector.get<AnyDeckClient>().getDiscordChannels(guildId);
    if (mounted) setState(() => _dcChannels = c);
  }

  Future<void> _loadParticipants() async {
    final s = await Injector.get<AnyDeckClient>().getDiscordState();
    final list = (s?['participants'] as List?) ?? [];
    if (mounted) {
      setState(() => _dcParticipants =
          list.map((e) => Map<String, dynamic>.from(e as Map)).toList());
    }
  }

  /// Campos da ação de Discord (operação + sub-campos conforme a operação).
  List<Widget> _buildDiscordFields() {
    final op = _discordOp;
    final fields = <Widget>[
      DropdownButtonFormField<String>(
        value: _discordOp,
        isExpanded: true,
        decoration: const InputDecoration(
            labelText: 'Ação do Discord', border: OutlineInputBorder()),
        items: const [
          DropdownMenuItem(
              value: 'toggleMute', child: Text('Microfone (mute on/off)')),
          DropdownMenuItem(
              value: 'toggleDeafen', child: Text('Ensurdecer (deafen on/off)')),
          DropdownMenuItem(
              value: 'joinChannel', child: Text('Entrar em canal de voz')),
          DropdownMenuItem(
              value: 'disconnect', child: Text('Desconectar da voz')),
          DropdownMenuItem(
              value: 'inputVolumeUp', child: Text('Volume do mic  +')),
          DropdownMenuItem(
              value: 'inputVolumeDown', child: Text('Volume do mic  −')),
          DropdownMenuItem(
              value: 'outputVolumeUp', child: Text('Volume de saída  +')),
          DropdownMenuItem(
              value: 'outputVolumeDown', child: Text('Volume de saída  −')),
          DropdownMenuItem(
              value: 'toggleVoiceMode', child: Text('Alternar PTT / Voz')),
          DropdownMenuItem(
              value: 'userMute', child: Text('Mutar usuário (toggle)')),
          DropdownMenuItem(
              value: 'userVolume', child: Text('Volume de usuário')),
        ],
        onChanged: (v) {
          setState(() => _discordOp = v ?? 'toggleMute');
          _loadDiscordDataFor(_discordOp);
        },
      ),
      const SizedBox(height: 12),
    ];

    if (op == 'joinChannel') {
      fields.addAll([
        DropdownButtonFormField<String>(
          value: _dcGuilds.any((g) => g['id'] == _dcGuildId) ? _dcGuildId : null,
          isExpanded: true,
          decoration: InputDecoration(
            labelText: 'Servidor',
            border: const OutlineInputBorder(),
            helperText: _dcGuilds.isEmpty ? 'Carregando…' : null,
          ),
          items: _dcGuilds
              .map((g) => DropdownMenuItem(
                  value: g['id'],
                  child: Text(g['name'] ?? '', overflow: TextOverflow.ellipsis)))
              .toList(),
          onChanged: (v) {
            setState(() {
              _dcGuildId = v;
              _dcChannels = [];
            });
            if (v != null) _loadChannels(v);
          },
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          value:
              _dcChannels.any((c) => c['id'] == _dcChannelId) ? _dcChannelId : null,
          isExpanded: true,
          decoration: const InputDecoration(
              labelText: 'Canal de voz', border: OutlineInputBorder()),
          items: _dcChannels
              .map((c) => DropdownMenuItem(
                  value: c['id'],
                  child: Text(c['name'] ?? '', overflow: TextOverflow.ellipsis)))
              .toList(),
          onChanged: (v) => setState(() {
            _dcChannelId = v;
            _dcChannelName = _dcChannels
                .firstWhere((c) => c['id'] == v, orElse: () => {})['name'];
          }),
        ),
        if (_dcChannelName != null && _dcChannelName!.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 6),
            child: Text('Canal salvo: ${_dcChannelName!}',
                style: const TextStyle(fontSize: 11, color: Colors.grey)),
          ),
      ]);
    } else if (op == 'userMute' || op == 'userVolume') {
      fields.addAll([
        DropdownButtonFormField<String>(
          value: _dcParticipants.any((p) => '${p['id']}' == _dcUserId)
              ? _dcUserId
              : null,
          isExpanded: true,
          decoration: InputDecoration(
            labelText: 'Usuário na call',
            border: const OutlineInputBorder(),
            helperText:
                _dcParticipants.isEmpty ? 'Entre numa call pra listar' : null,
          ),
          items: _dcParticipants.map((p) {
            final name = (p['nick'] ?? p['username'] ?? p['id']).toString();
            return DropdownMenuItem(
                value: '${p['id']}',
                child: Text(name, overflow: TextOverflow.ellipsis));
          }).toList(),
          onChanged: (v) => setState(() {
            _dcUserId = v;
            final pp = _dcParticipants
                .firstWhere((p) => '${p['id']}' == v, orElse: () => {});
            _dcUserName = (pp['nick'] ?? pp['username'])?.toString();
          }),
        ),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            onPressed: _loadParticipants,
            icon: const Icon(Icons.refresh, size: 16),
            label: const Text('Atualizar lista'),
          ),
        ),
        if (op == 'userVolume') ...[
          Text('Volume: $_dcValue%'),
          Slider(
            value: _dcValue.clamp(0, 200).toDouble(),
            min: 0,
            max: 200,
            divisions: 40,
            label: '$_dcValue%',
            onChanged: (v) => setState(() => _dcValue = v.round()),
          ),
        ],
      ]);
    } else if (op.toLowerCase().contains('volume')) {
      fields.add(Row(
        children: [
          const Text('Passo: '),
          Expanded(
            child: Slider(
              value: _dcDelta.clamp(1, 50).toDouble(),
              min: 1,
              max: 50,
              divisions: 49,
              label: '$_dcDelta%',
              onChanged: (v) => setState(() => _dcDelta = v.round()),
            ),
          ),
          Text('$_dcDelta%'),
        ],
      ));
    }

    fields.add(const Padding(
      padding: EdgeInsets.only(top: 6),
      child: Text('Requer o Discord conectado no PC (config do AnyDeck).',
          style: TextStyle(fontSize: 11, color: Colors.grey)),
    ));
    return fields;
  }

  /// Mostra lado a lado como o botão fica desligado x ativo (com a cor escolhida).
  Widget _buildTogglePreview() {
    final icons = _previewIcons();
    Widget chip(String label, Color color, IconData icon) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(8),
              boxShadow: const [
                BoxShadow(blurRadius: 3, color: Colors.black38)
              ],
            ),
            child: Icon(icon, color: Colors.white, size: 26),
          ),
          const SizedBox(height: 4),
          Text(label,
              style: const TextStyle(fontSize: 11, color: Colors.white70)),
        ],
      );
    }

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        color: Colors.black26,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceEvenly,
        children: [
          chip('Desligado', _selectedColor, icons.$1),
          const Icon(Icons.arrow_forward, color: Colors.white38, size: 20),
          chip('Ativo', _activeColor, icons.$2),
        ],
      ),
    );
  }

  /// (ícone desligado, ícone ativo) conforme a ação — espelha o runtime.
  (IconData, IconData) _previewIcons() {
    if (_selectedActionType == 'discord') {
      return _discordOp == 'toggleDeafen'
          ? (Icons.headset_mic, Icons.headset_off)
          : (Icons.mic, Icons.mic_off);
    }
    return (Icons.volume_up, Icons.volume_off);
  }

  Widget _buildIconOption(String? iconName) {
    final isSelected = _selectedIcon == iconName;
    return InkWell(
      onTap: () {
        setState(() {
          _selectedIcon = iconName;
          if (iconName != null)
            _iconBase64 = null; // Clear custom image if icon selected
        });
      },
      child: Container(
        width: 40,
        height: 40,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: isSelected ? Colors.blue.withValues(alpha: 0.3) : Colors.grey[800],
          borderRadius: BorderRadius.circular(4),
          border: isSelected
              ? Border.all(color: Colors.blue)
              : Border.all(color: Colors.white10),
        ),
        child: iconName == null
            ? const Icon(Icons.block, color: Colors.white54)
            : Icon(_getIconData(iconName), color: Colors.white),
      ),
    );
  }

  IconData _getIconData(String name) {
    switch (name.toLowerCase()) {
      case 'play':
        return Icons.play_arrow;
      case 'pause':
        return Icons.pause;
      case 'volume_up':
        return Icons.volume_up;
      case 'volume_off':
        return Icons.volume_off;
      case 'mic':
        return Icons.mic;
      case 'mic_off':
        return Icons.mic_off;
      case 'keyboard':
        return Icons.keyboard;
      case 'launch':
        return Icons.rocket_launch;
      case 'home':
        return Icons.home;
      case 'settings':
        return Icons.settings;
      case 'code':
        return Icons.code;
      case 'videocam':
        return Icons.videocam;
      case 'call_end':
        return Icons.call_end;
      case 'folder':
        return Icons.folder;
      case 'save':
        return Icons.save;
      case 'delete':
        return Icons.delete;
      default:
        return Icons.extension;
    }
  }
}
