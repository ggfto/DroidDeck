import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/src/config/discord_settings_page.dart';
import 'package:companion/src/config/obs_settings_page.dart';
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
  // Mixer: alvo do botão — 'app' (sessão de um processo) ou 'device' (dispositivo do Windows)
  String _mixerTarget = 'app';
  // 'default' = dispositivo padrão do Windows. É o valor recomendado: o id do endpoint muda
  // quando o usuário troca de fone/monitor e o botão passaria a apontar pra nada.
  String _mixerDeviceId = 'default';
  String _mixerDeviceKind = 'output'; // output | input
  int _mixerVolume = 50; // usado com setVolume
  int _mixerStep = 5; // passo do volumeUp/volumeDown
  List<MixerEntity> _mixerOutputs = [];
  List<MixerEntity> _mixerInputs = [];
  bool _mixerDevicesLoading = false;
  String _discordOp = 'toggleMute';
  String _mediaCommand = 'playpause';
  String _tuyaOp = 'toggle';
  String? _tuyaDeviceId;
  String? _tuyaCode;
  String _tuyaValue = '';
  String _obsOp = 'setScene';
  String? _obsScene;
  String? _obsInput;
  // Soundboard
  String _soundOperation = 'play'; // play | stop
  String _soundSource = 'myinstants'; // myinstants | discord
  String? _soundId;
  String? _soundUrl;
  String? _soundTitle;
  final TextEditingController _soundSearchController = TextEditingController();
  List<SoundResult> _soundResults = [];
  bool _soundSearching = false;
  // Soundboard nativa do Discord
  String? _dcSoundId;
  String? _dcSoundGuildId;
  List<Map<String, String>> _dcSounds = [];
  bool _dcSoundsLoading = false;
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
      _selectedActionType == 'discord' ||
      _selectedActionType == 'mixer' ||
      _selectedActionType == 'tuya';

  final List<String> _actionTypes = [
    'none',
    'hotkey',
    'launch_app',
    'media',
    'mixer',
    'open_profile',
    'back',
    'multi',
    'discord',
    'obs',
    'soundboard',
    'tuya',
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
      } else if (_selectedActionType == 'media') {
        _actionParamController = TextEditingController();
        _mediaCommand =
            widget.button.action!.parameters['command'] ?? 'playpause';
      } else if (_selectedActionType == 'mixer') {
        final mp = widget.button.action!.parameters;
        _actionParamController =
            TextEditingController(text: mp['processName'] ?? '');
        _mixerOperation = mp['operation'] ?? 'toggleMute';
        _mixerTarget = (mp['deviceId'] ?? '').isNotEmpty ? 'device' : 'app';
        _mixerDeviceId = (mp['deviceId'] ?? '').isNotEmpty
            ? mp['deviceId']!
            : 'default';
        _mixerDeviceKind = mp['deviceKind'] ?? 'output';
        _mixerVolume = int.tryParse(mp['volume'] ?? '') ?? 50;
        _mixerStep = int.tryParse(mp['step'] ?? '') ?? 5;
        if (_mixerTarget == 'device') _loadMixerDevices();
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
      } else if (_selectedActionType == 'obs') {
        _actionParamController = TextEditingController();
        final dp = widget.button.action!.parameters;
        _obsOp = dp['operation'] ?? 'setScene';
        _obsScene = dp['scene'];
        _obsInput = dp['inputName'];
        _loadObsState();
      } else if (_selectedActionType == 'tuya') {
        _actionParamController = TextEditingController();
        final dp = widget.button.action!.parameters;
        _tuyaOp = dp['operation'] ?? 'toggle';
        _tuyaDeviceId = dp['deviceId'];
        _tuyaCode = dp['code'];
        _tuyaValue = dp['value'] ?? '';
        _loadTuyaState();
      } else if (_selectedActionType == 'soundboard') {
        _actionParamController = TextEditingController();
        final dp = widget.button.action!.parameters;
        _soundOperation = dp['operation'] ?? 'play';
        _soundSource = dp['source'] ?? 'myinstants';
        _soundId = dp['id'];
        _soundUrl = dp['url'];
        _soundTitle = dp['title'];
        _dcSoundId = dp['soundId'];
        _dcSoundGuildId = dp['guildId'];
        if (_soundSource == 'discord') _loadDiscordSounds();
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
    _soundSearchController.dispose();
    super.dispose();
  }

  void _save() {
    Map<String, String> params = {};
    if (_selectedActionType == 'hotkey') {
      params['keys'] = _actionParamController.text;
    } else if (_selectedActionType == 'launch_app') {
      params['path'] = _actionParamController.text;
    } else if (_selectedActionType == 'media') {
      params['command'] = _mediaCommand;
    } else if (_selectedActionType == 'obs') {
      params['operation'] = _obsOp;
      if (_obsOp == 'setScene' && _obsScene != null) params['scene'] = _obsScene!;
      if (_obsOp == 'toggleInputMute' && _obsInput != null) {
        params['inputName'] = _obsInput!;
      }
    } else if (_selectedActionType == 'tuya') {
      params['operation'] = _tuyaOp;
      if (_tuyaDeviceId != null) params['deviceId'] = _tuyaDeviceId!;
      if (_tuyaCode != null) params['code'] = _tuyaCode!;
      if (_tuyaOp == 'set') {
        params['value'] = _tuyaValue;
        // O backend precisa do tipo para converter a string no que a Tuya espera
        // (mandar "true" onde ela quer booleano faz a API recusar).
        params['valueType'] = _tuyaFunction()?['type'] ?? 'String';
      }
    } else if (_selectedActionType == 'mixer') {
      params['operation'] = _mixerOperation;
      if (_mixerTarget == 'device') {
        params['deviceId'] = _mixerDeviceId;
        params['deviceKind'] = _mixerDeviceKind;
        if (_mixerOperation == 'setVolume') {
          params['volume'] = '$_mixerVolume';
        } else if (_mixerOperation == 'volumeUp' ||
            _mixerOperation == 'volumeDown') {
          params['step'] = '$_mixerStep';
        }
      } else {
        params['processName'] = _actionParamController.text.trim();
      }
    } else if (_selectedActionType == 'open_profile') {
      if (_targetProfileId != null) params['profileId'] = _targetProfileId!;
    } else if (_selectedActionType == 'multi') {
      params['steps'] = _encodeSteps(_multiSteps);
    } else if (_selectedActionType == 'soundboard') {
      params['operation'] = _soundOperation;
      if (_soundOperation == 'play') {
        params['source'] = _soundSource;
        if (_soundSource == 'discord') {
          if (_dcSoundId != null) params['soundId'] = _dcSoundId!;
          if (_dcSoundGuildId != null && _dcSoundGuildId!.isNotEmpty) {
            params['guildId'] = _dcSoundGuildId!;
          }
          if (_soundTitle != null) params['title'] = _soundTitle!;
        } else {
          if (_soundId != null) params['id'] = _soundId!;
          if (_soundUrl != null) params['url'] = _soundUrl!;
          if (_soundTitle != null) params['title'] = _soundTitle!;
        }
      }
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
                    // Num botão novo o initState não passou pelo ramo da Tuya, então
                    // sem isto os campos abririam dizendo "conta não vinculada" mesmo
                    // com a conta pareada — o snapshot só chega em broadcast raro.
                    if (val == 'tuya') _loadTuyaState();
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
                if (_selectedActionType == 'media') ...[
                  DropdownButtonFormField<String>(
                    value: _mediaCommand,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Comando de mídia',
                      border: OutlineInputBorder(),
                    ),
                    items: const [
                      DropdownMenuItem(
                          value: 'playpause', child: Text('Play / Pause')),
                      DropdownMenuItem(
                          value: 'next', child: Text('Próxima faixa')),
                      DropdownMenuItem(
                          value: 'previous', child: Text('Faixa anterior')),
                      DropdownMenuItem(value: 'stop', child: Text('Parar')),
                      DropdownMenuItem(value: 'play', child: Text('Play')),
                      DropdownMenuItem(value: 'pause', child: Text('Pause')),
                    ],
                    onChanged: (v) =>
                        setState(() => _mediaCommand = v ?? 'playpause'),
                  ),
                  const Padding(
                    padding: EdgeInsets.only(top: 6),
                    child: Text(
                      'Controla a mídia que está tocando no PC (Spotify, YouTube, navegador…).',
                      style: TextStyle(fontSize: 11, color: Colors.grey),
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
                if (_selectedActionType == 'obs') ...[
                  ..._buildObsFields(),
                ],
                if (_selectedActionType == 'soundboard') ...[
                  ..._buildSoundboardFields(),
                ],
                if (_selectedActionType == 'tuya') ...[
                  ..._buildTuyaFields(),
                ],
                if (_selectedActionType == 'mixer') ...[
                  ..._buildMixerFields(),
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
    // Atualiza configured/connected (pro aviso) no signal compartilhado.
    Injector.get<DroidDeckClient>().getDiscordState().then((s) {
      if (s != null && mounted) {
        Injector.get<SignalRService>().discordState.value =
            Map<String, dynamic>.from(s);
      }
    }).catchError((_) {});
    if (op == 'joinChannel' && _dcGuilds.isEmpty) _loadGuilds();
    if ((op == 'userMute' || op == 'userVolume') && _dcParticipants.isEmpty) {
      _loadParticipants();
    }
  }

  /// Aviso quando a ação Discord depende de algo ainda não pronto
  /// (app não configurado, ou configurado mas desconectado).
  Widget _discordWarning() {
    return Watch((context) {
      final ds = Injector.get<SignalRService>().discordState.value;
      final configured = ds['configured'] == true;
      final connected = ds['connected'] == true;
      if (configured && connected) return const SizedBox.shrink();

      final c = configured ? Colors.blueGrey : Colors.orange;
      final msg = configured
          ? 'Discord configurado, mas desconectado. Abra o Discord no PC e conecte.'
          : 'Discord não configurado — esta ação não vai funcionar até configurar o app do Discord.';
      return Container(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: c.withValues(alpha: 0.18),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: c.withValues(alpha: 0.5)),
        ),
        child: Row(
          children: [
            Icon(
                configured
                    ? Icons.link_off
                    : Icons.warning_amber_rounded,
                color: c,
                size: 20),
            const SizedBox(width: 8),
            Expanded(child: Text(msg, style: const TextStyle(fontSize: 12))),
            TextButton(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(
                    builder: (_) => const DiscordSettingsPage()),
              ),
              child: const Text('Configurar'),
            ),
          ],
        ),
      );
    });
  }

  Future<void> _loadGuilds() async {
    final g = await Injector.get<DroidDeckClient>().getDiscordGuilds();
    if (mounted) setState(() => _dcGuilds = g);
  }

  Future<void> _loadChannels(String guildId) async {
    final c = await Injector.get<DroidDeckClient>().getDiscordChannels(guildId);
    if (mounted) setState(() => _dcChannels = c);
  }

  Future<void> _loadParticipants() async {
    final s = await Injector.get<DroidDeckClient>().getDiscordState();
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
      _discordWarning(),
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
      child: Text('Requer o Discord conectado no PC (config do DroidDeck).',
          style: TextStyle(fontSize: 11, color: Colors.grey)),
    ));
    return fields;
  }

  // ---- OBS ----
  Future<void> _runSoundSearch() async {
    final q = _soundSearchController.text.trim();
    if (q.isEmpty) return;
    setState(() => _soundSearching = true);
    try {
      final results = await Injector.get<DroidDeckClient>().searchSounds(q);
      if (mounted) setState(() => _soundResults = results);
    } finally {
      if (mounted) setState(() => _soundSearching = false);
    }
  }

  List<Widget> _buildSoundboardFields() {
    final fields = <Widget>[
      DropdownButtonFormField<String>(
        value: _soundOperation,
        isExpanded: true,
        decoration: const InputDecoration(
            labelText: 'Ação', border: OutlineInputBorder()),
        items: const [
          DropdownMenuItem(value: 'play', child: Text('Tocar um som')),
          DropdownMenuItem(value: 'stop', child: Text('Parar tudo')),
        ],
        onChanged: (v) => setState(() => _soundOperation = v ?? 'play'),
      ),
      const SizedBox(height: 12),
    ];

    if (_soundOperation == 'stop') {
      fields.add(const Padding(
        padding: EdgeInsets.only(top: 6),
        child: Text('Para qualquer som que estiver tocando na soundboard.',
            style: TextStyle(fontSize: 11, color: Colors.grey)),
      ));
      return fields;
    }

    // operação "play": escolhe a fonte do som
    fields.add(DropdownButtonFormField<String>(
      value: _soundSource,
      isExpanded: true,
      decoration: const InputDecoration(
          labelText: 'Fonte', border: OutlineInputBorder()),
      items: const [
        DropdownMenuItem(
            value: 'myinstants',
            child: Text('MyInstants (toca no PC → cabo/OBS)')),
        DropdownMenuItem(
            value: 'discord',
            child: Text('Soundboard do Discord (canal de voz)')),
      ],
      onChanged: (v) {
        setState(() {
          _soundSource = v ?? 'myinstants';
          // A seleção pertence a uma fonte específica: limpa ao trocar.
          _soundTitle = null;
          _soundId = null;
          _soundUrl = null;
          _dcSoundId = null;
          _dcSoundGuildId = null;
        });
        if (_soundSource == 'discord' && _dcSounds.isEmpty) _loadDiscordSounds();
      },
    ));
    fields.add(const SizedBox(height: 12));

    fields.addAll(
        _soundSource == 'discord' ? _discordSoundFields() : _myInstantsFields());
    return fields;
  }

  List<Widget> _myInstantsFields() {
    final fields = <Widget>[];
    if (_soundTitle != null && _soundTitle!.isNotEmpty) {
      fields.add(Card(
        color: Colors.green.withValues(alpha: 0.12),
        child: ListTile(
          leading: const Icon(Icons.music_note, color: Colors.green),
          title: Text(_soundTitle!, overflow: TextOverflow.ellipsis),
          subtitle: const Text('Som selecionado'),
          trailing: IconButton(
            icon: const Icon(Icons.play_arrow),
            tooltip: 'Testar no PC',
            onPressed: (_soundUrl == null)
                ? null
                : () => Injector.get<DroidDeckClient>()
                    .playSound(_soundId ?? '', _soundUrl!, _soundTitle ?? ''),
          ),
        ),
      ));
      fields.add(const SizedBox(height: 8));
    }

    fields.add(Row(
      children: [
        Expanded(
          child: TextField(
            controller: _soundSearchController,
            textInputAction: TextInputAction.search,
            decoration: const InputDecoration(
              labelText: 'Buscar som no MyInstants',
              hintText: 'ex.: risada, bruh, aplausos…',
              border: OutlineInputBorder(),
            ),
            onSubmitted: (_) => _runSoundSearch(),
          ),
        ),
        const SizedBox(width: 8),
        _soundSearching
            ? const Padding(
                padding: EdgeInsets.all(8),
                child: SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2)),
              )
            : IconButton.filled(
                icon: const Icon(Icons.search),
                onPressed: _runSoundSearch,
              ),
      ],
    ));
    fields.add(const SizedBox(height: 8));

    if (_soundResults.isNotEmpty) {
      fields.add(ConstrainedBox(
        constraints: const BoxConstraints(maxHeight: 260),
        child: ListView.builder(
          shrinkWrap: true,
          itemCount: _soundResults.length,
          itemBuilder: (context, i) {
            final s = _soundResults[i];
            final selected = s.mp3 == _soundUrl;
            return ListTile(
              dense: true,
              selected: selected,
              leading: IconButton(
                icon: const Icon(Icons.play_circle_outline),
                tooltip: 'Ouvir (toca no PC)',
                onPressed: () => Injector.get<DroidDeckClient>()
                    .playSound(s.id, s.mp3, s.title),
              ),
              title: Text(s.title, overflow: TextOverflow.ellipsis),
              trailing: selected
                  ? const Icon(Icons.check_circle, color: Colors.green)
                  : const Icon(Icons.add_circle_outline),
              onTap: () => setState(() {
                _soundId = s.id;
                _soundUrl = s.mp3;
                _soundTitle = s.title;
                if (_labelController.text.trim().isEmpty) {
                  _labelController.text = s.title;
                }
              }),
            );
          },
        ),
      ));
    }

    fields.add(const Padding(
      padding: EdgeInsets.only(top: 6),
      child: Text(
          'O som toca no PC e sai pelo dispositivo configurado (cabo p/ Discord/OBS + monitor). '
          'Configure a saída em Configurações › Soundboard.',
          style: TextStyle(fontSize: 11, color: Colors.grey)),
    ));
    return fields;
  }

  List<Widget> _loadDiscordSoundsButtonRow() {
    return [
      Row(
        children: [
          const Expanded(
            child: Text('Sons da soundboard do Discord',
                style: TextStyle(fontWeight: FontWeight.bold)),
          ),
          _dcSoundsLoading
              ? const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2))
              : IconButton(
                  icon: const Icon(Icons.refresh),
                  tooltip: 'Recarregar',
                  onPressed: _loadDiscordSounds,
                ),
        ],
      ),
      const SizedBox(height: 8),
    ];
  }

  List<Widget> _discordSoundFields() {
    final fields = <Widget>[];

    if (_soundTitle != null && _soundTitle!.isNotEmpty) {
      fields.add(Card(
        color: Colors.green.withValues(alpha: 0.12),
        child: ListTile(
          leading: const Icon(Icons.graphic_eq, color: Colors.green),
          title: Text(_soundTitle!, overflow: TextOverflow.ellipsis),
          subtitle: const Text('Som selecionado'),
          trailing: IconButton(
            icon: const Icon(Icons.play_arrow),
            tooltip: 'Testar no canal de voz',
            onPressed: (_dcSoundId == null)
                ? null
                : () => Injector.get<DroidDeckClient>()
                    .playDiscordSoundboard(_dcSoundId!, _dcSoundGuildId),
          ),
        ),
      ));
      fields.add(const SizedBox(height: 8));
    }

    fields.addAll(_loadDiscordSoundsButtonRow());

    if (_dcSounds.isEmpty && !_dcSoundsLoading) {
      fields.add(const Text(
        'Nenhum som carregado. Conecte o Discord no PC, entre num canal de voz e toque em recarregar.',
        style: TextStyle(fontSize: 12, color: Colors.grey),
      ));
    } else if (_dcSounds.isNotEmpty) {
      fields.add(ConstrainedBox(
        constraints: const BoxConstraints(maxHeight: 260),
        child: ListView.builder(
          shrinkWrap: true,
          itemCount: _dcSounds.length,
          itemBuilder: (context, i) {
            final s = _dcSounds[i];
            final sid = s['soundId'] ?? '';
            final name = s['name'] ?? '';
            final emoji = s['emojiName'] ?? '';
            final selected = sid == _dcSoundId;
            return ListTile(
              dense: true,
              selected: selected,
              leading: IconButton(
                icon: const Icon(Icons.play_circle_outline),
                tooltip: 'Ouvir (no canal de voz)',
                onPressed: () => Injector.get<DroidDeckClient>()
                    .playDiscordSoundboard(sid, s['guildId']),
              ),
              title: Text('${emoji.isNotEmpty ? '$emoji  ' : ''}$name',
                  overflow: TextOverflow.ellipsis),
              trailing: selected
                  ? const Icon(Icons.check_circle, color: Colors.green)
                  : const Icon(Icons.add_circle_outline),
              onTap: () => setState(() {
                _dcSoundId = sid;
                _dcSoundGuildId = s['guildId'];
                _soundTitle = name;
                if (_labelController.text.trim().isEmpty) {
                  _labelController.text = name;
                }
              }),
            );
          },
        ),
      ));
    }

    fields.add(const Padding(
      padding: EdgeInsets.only(top: 6),
      child: Text(
          'Toca a soundboard NATIVA do Discord no canal de voz atual (sem cabo). '
          'Você precisa estar num canal de voz. Não sai na live/OBS.',
          style: TextStyle(fontSize: 11, color: Colors.grey)),
    ));
    return fields;
  }

  Future<void> _loadDiscordSounds() async {
    setState(() => _dcSoundsLoading = true);
    try {
      final sounds =
          await Injector.get<DroidDeckClient>().getDiscordSoundboardSounds();
      if (mounted) setState(() => _dcSounds = sounds);
    } finally {
      if (mounted) setState(() => _dcSoundsLoading = false);
    }
  }

  // ---- Tuya / Smart Life ----

  void _loadTuyaState() {
    Injector.get<DroidDeckClient>().getTuyaState().then((s) {
      if (s != null && mounted) {
        setState(() {
          Injector.get<SignalRService>().tuyaState.value =
              Map<String, dynamic>.from(s);
        });
      }
    }).catchError((_) {});
  }

  List<dynamic> _tuyaDevices() =>
      (Injector.get<SignalRService>().tuyaState.value['devices'] as List? ??
          const []);

  Map<String, dynamic>? _tuyaDevice() {
    for (final d in _tuyaDevices()) {
      if ((d as Map)['id'] == _tuyaDeviceId) {
        return Map<String, dynamic>.from(d);
      }
    }
    return null;
  }

  /// Metadados da funcao escolhida (type/values), vindos do `specifications` do
  /// proprio aparelho -- e o que faz o campo de valor se adaptar sozinho.
  Map<String, dynamic>? _tuyaFunction() {
    final device = _tuyaDevice();
    if (device == null || _tuyaCode == null) return null;
    final fns = device['functions'] as Map? ?? const {};
    final fn = fns[_tuyaCode];
    return fn == null ? null : Map<String, dynamic>.from(fn as Map);
  }

  /// Os limites vem como JSON cru da Tuya: {"min":10,"max":1000} ou {"range":[...]}.
  Map<String, dynamic> _tuyaValues() {
    final raw = _tuyaFunction()?['values'];
    if (raw is! String || raw.isEmpty) return const {};
    try {
      return Map<String, dynamic>.from(jsonDecode(raw) as Map);
    } catch (_) {
      return const {};
    }
  }

  // ---- Mixer (volume/mudo de app ou de dispositivo do Windows) ----

  /// Operações que existem para os dois alvos. Volume só faz sentido no dispositivo:
  /// o IAudioControlService do backend só expõe mudo por processo.
  static const _mixerMuteOps = ['toggleMute', 'mute', 'unmute'];

  List<MixerEntity> get _mixerDevices =>
      _mixerDeviceKind == 'input' ? _mixerInputs : _mixerOutputs;

  Future<void> _loadMixerDevices() async {
    if (_mixerDevicesLoading) return;
    setState(() => _mixerDevicesLoading = true);
    try {
      final client = Injector.get<DroidDeckClient>();
      final outputs = await client.getOutputs();
      final inputs = await client.getInputs();
      if (!mounted) return;
      setState(() {
        _mixerOutputs = outputs;
        _mixerInputs = inputs;
      });
    } catch (_) {
      // Sem a lista o botão ainda funciona apontando pro dispositivo padrão.
    } finally {
      if (mounted) setState(() => _mixerDevicesLoading = false);
    }
  }

  String _mixerDeviceLabel(MixerEntity e) =>
      e.device.title?.trim().isNotEmpty == true
          ? e.device.title!.trim()
          : (e.id ?? 'Dispositivo');

  List<Widget> _buildMixerFields() {
    final isDevice = _mixerTarget == 'device';
    final devices = _mixerDevices;
    final knownIds = {'default', ...devices.map((d) => d.id ?? '')};

    return [
      DropdownButtonFormField<String>(
        value: _mixerTarget,
        decoration: const InputDecoration(
          labelText: 'Alvo',
          border: OutlineInputBorder(),
        ),
        items: const [
          DropdownMenuItem(
              value: 'app', child: Text('Aplicativo (processo)')),
          DropdownMenuItem(
              value: 'device', child: Text('Dispositivo do Windows')),
        ],
        onChanged: (val) {
          setState(() {
            _mixerTarget = val ?? 'app';
            if (_mixerTarget == 'app' &&
                !_mixerMuteOps.contains(_mixerOperation)) {
              _mixerOperation = 'toggleMute';
            }
          });
          if (_mixerTarget == 'device') _loadMixerDevices();
        },
      ),
      const SizedBox(height: 16),
      if (isDevice) ...[
        DropdownButtonFormField<String>(
          value: _mixerDeviceKind,
          decoration: const InputDecoration(
            labelText: 'Tipo',
            border: OutlineInputBorder(),
          ),
          items: const [
            DropdownMenuItem(
                value: 'output', child: Text('Saída (alto-falante/fone)')),
            DropdownMenuItem(
                value: 'input', child: Text('Entrada (microfone)')),
          ],
          onChanged: (val) => setState(() {
            _mixerDeviceKind = val ?? 'output';
            // O id salvo pertence à outra lista; volta pro padrão.
            _mixerDeviceId = 'default';
          }),
        ),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          value: knownIds.contains(_mixerDeviceId) ? _mixerDeviceId : null,
          decoration: InputDecoration(
            labelText: 'Dispositivo',
            border: const OutlineInputBorder(),
            helperText: _mixerDeviceId == 'default'
                ? 'Acompanha a troca de fone/monitor'
                : 'Id fixo: se você trocar de dispositivo, o botão para de valer',
          ),
          // Um id salvo que não está na lista (dispositivo desconectado) continua
          // selecionável, senão salvar de novo trocaria a configuração sem avisar.
          items: [
            const DropdownMenuItem(
                value: 'default', child: Text('Padrão do Windows')),
            ...devices.map((d) => DropdownMenuItem(
                  value: d.id,
                  child: Text(_mixerDeviceLabel(d),
                      overflow: TextOverflow.ellipsis),
                )),
            if (!knownIds.contains(_mixerDeviceId))
              DropdownMenuItem(
                value: _mixerDeviceId,
                child: const Text('Dispositivo salvo (desconectado)'),
              ),
          ],
          onChanged: (val) =>
              setState(() => _mixerDeviceId = val ?? 'default'),
        ),
        if (_mixerDevicesLoading)
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: LinearProgressIndicator(minHeight: 2),
          ),
        const SizedBox(height: 16),
      ],
      DropdownButtonFormField<String>(
        value: _mixerOperation,
        decoration: const InputDecoration(
          labelText: 'Operação',
          border: OutlineInputBorder(),
        ),
        items: [
          const DropdownMenuItem(
              value: 'toggleMute', child: Text('Alternar mudo')),
          const DropdownMenuItem(value: 'mute', child: Text('Mutar')),
          const DropdownMenuItem(value: 'unmute', child: Text('Desmutar')),
          if (isDevice) ...[
            const DropdownMenuItem(
                value: 'volumeUp', child: Text('Volume +')),
            const DropdownMenuItem(
                value: 'volumeDown', child: Text('Volume −')),
            const DropdownMenuItem(
                value: 'setVolume', child: Text('Definir volume')),
          ],
        ],
        onChanged: (val) =>
            setState(() => _mixerOperation = val ?? 'toggleMute'),
      ),
      if (isDevice && _mixerOperation == 'setVolume') ...[
        const SizedBox(height: 16),
        Text('Volume: $_mixerVolume%'),
        Slider(
          value: _mixerVolume.toDouble(),
          min: 0,
          max: 100,
          divisions: 100,
          label: '$_mixerVolume%',
          onChanged: (v) => setState(() => _mixerVolume = v.round()),
        ),
      ],
      if (isDevice &&
          (_mixerOperation == 'volumeUp' ||
              _mixerOperation == 'volumeDown')) ...[
        const SizedBox(height: 16),
        Text('Passo: $_mixerStep pontos'),
        Slider(
          value: _mixerStep.toDouble(),
          min: 1,
          max: 25,
          divisions: 24,
          label: '$_mixerStep',
          onChanged: (v) => setState(() => _mixerStep = v.round()),
        ),
      ],
      if (!isDevice) ...[
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
    ];
  }

  List<Widget> _buildTuyaFields() {
    final paired =
        Injector.get<SignalRService>().tuyaState.value['paired'] == true;
    final devices = _tuyaDevices();

    if (!paired) {
      return [
        Card(
          color: Colors.orange.withValues(alpha: 0.12),
          child: const Padding(
            padding: EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Conta Tuya nao vinculada',
                    style: TextStyle(fontWeight: FontWeight.bold)),
                SizedBox(height: 4),
                Text(
                  'Vincule sua conta Smart Life em Configuracoes -> Casa inteligente '
                  'para escolher um dispositivo.',
                  style: TextStyle(fontSize: 12),
                ),
              ],
            ),
          ),
        ),
      ];
    }

    final device = _tuyaDevice();
    final fn = _tuyaFunction();
    final type = fn?['type'] as String?;

    return [
      DropdownButtonFormField<String>(
        value: devices.any((d) => (d as Map)['id'] == _tuyaDeviceId)
            ? _tuyaDeviceId
            : null,
        isExpanded: true,
        decoration: InputDecoration(
          labelText: 'Dispositivo',
          border: const OutlineInputBorder(),
          helperText: devices.isEmpty
              ? 'Nenhum dispositivo -- atualize em Configuracoes'
              : null,
        ),
        items: devices.map((d) {
          final dev = d as Map;
          return DropdownMenuItem(
            value: dev['id'] as String,
            child: Text(
              '${dev['name']}${dev['online'] == true ? '' : ' (offline)'}',
              overflow: TextOverflow.ellipsis,
            ),
          );
        }).toList(),
        onChanged: (v) => setState(() {
          _tuyaDeviceId = v;
          // Trocar de aparelho invalida a funcao: os DPs sao de cada modelo.
          _tuyaCode = null;
          _tuyaValue = '';
        }),
      ),
      const SizedBox(height: 12),
      if (device != null) ...[
        DropdownButtonFormField<String>(
          value:
              (device['functions'] as Map? ?? const {}).containsKey(_tuyaCode)
                  ? _tuyaCode
                  : null,
          isExpanded: true,
          decoration: const InputDecoration(
            labelText: 'O que controlar',
            border: OutlineInputBorder(),
          ),
          items: (device['functions'] as Map? ?? const {})
              .entries
              .map((e) => DropdownMenuItem(
                    value: e.key as String,
                    child: Text('${e.key}  -  ${(e.value as Map)['type']}',
                        overflow: TextOverflow.ellipsis),
                  ))
              .toList(),
          onChanged: (v) => setState(() {
            _tuyaCode = v;
            _tuyaValue = '';
            // So booleano pode alternar; o resto exige um valor explicito.
            final t = _tuyaFunction()?['type'];
            _tuyaOp = t == 'Boolean' ? 'toggle' : 'set';
          }),
        ),
        const SizedBox(height: 12),
      ],
      if (fn != null) ...[
        if (type == 'Boolean')
          DropdownButtonFormField<String>(
            value: _tuyaOp,
            decoration: const InputDecoration(
              labelText: 'Acao',
              border: OutlineInputBorder(),
            ),
            items: const [
              DropdownMenuItem(
                  value: 'toggle', child: Text('Alternar (liga/desliga)')),
              DropdownMenuItem(
                  value: 'set', child: Text('Sempre ligar / sempre desligar')),
            ],
            onChanged: (v) => setState(() => _tuyaOp = v ?? 'toggle'),
          ),
        if (type == 'Boolean' && _tuyaOp == 'set') ...[
          const SizedBox(height: 12),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(_tuyaValue == 'true' ? 'Ligar' : 'Desligar'),
            value: _tuyaValue == 'true',
            onChanged: (v) => setState(() => _tuyaValue = v ? 'true' : 'false'),
          ),
        ],
        if (type == 'Integer') ..._buildTuyaIntegerField(),
        if (type == 'Enum') ..._buildTuyaEnumField(),
        if (type == 'String' || type == 'Json' || type == 'Raw') ...[
          TextFormField(
            initialValue: _tuyaValue,
            decoration: InputDecoration(
              labelText: 'Valor',
              border: const OutlineInputBorder(),
              helperText: 'Tipo $type -- valor cru enviado a Tuya',
            ),
            onChanged: (v) => _tuyaValue = v,
          ),
        ],
        const SizedBox(height: 12),
        OutlinedButton.icon(
          onPressed: _testTuyaCommand,
          icon: const Icon(Icons.play_arrow),
          label: const Text('Testar agora'),
        ),
      ],
      const SizedBox(height: 8),
      const Text(
        'O estado do botao acompanha o aparelho em tempo real; use "Cor ativa" '
        'para ele acender quando estiver ligado.',
        style: TextStyle(fontSize: 11, color: Colors.grey),
      ),
    ];
  }

  List<Widget> _buildTuyaIntegerField() {
    final values = _tuyaValues();
    final min = (values['min'] as num?)?.toDouble() ?? 0;
    final max = (values['max'] as num?)?.toDouble() ?? 100;
    final unit = values['unit'] as String? ?? '';
    final current = double.tryParse(_tuyaValue) ?? min;
    final clamped = current.clamp(min, max);

    return [
      const SizedBox(height: 12),
      Text('Valor: ${clamped.round()}$unit',
          style: const TextStyle(fontSize: 12)),
      Slider(
        min: min,
        max: max,
        // Faixas grandes (brilho vai de 10 a 1000) ficariam travadas com poucas
        // divisoes; sem divisions o slider e continuo e arredondamos no fim.
        value: clamped.toDouble(),
        label: '${clamped.round()}',
        onChanged: (v) => setState(() => _tuyaValue = '${v.round()}'),
      ),
      Text('Faixa: ${min.round()} a ${max.round()}$unit',
          style: const TextStyle(fontSize: 11, color: Colors.grey)),
    ];
  }

  List<Widget> _buildTuyaEnumField() {
    final range =
        (_tuyaValues()['range'] as List? ?? const []).map((e) => '$e').toList();

    return [
      const SizedBox(height: 12),
      DropdownButtonFormField<String>(
        value: range.contains(_tuyaValue) ? _tuyaValue : null,
        isExpanded: true,
        decoration: const InputDecoration(
          labelText: 'Valor',
          border: OutlineInputBorder(),
        ),
        items:
            range.map((v) => DropdownMenuItem(value: v, child: Text(v))).toList(),
        onChanged: (v) => setState(() => _tuyaValue = v ?? ''),
      ),
    ];
  }

  Future<void> _testTuyaCommand() async {
    if (_tuyaDeviceId == null || _tuyaCode == null) return;

    final type = _tuyaFunction()?['type'];
    Object? value;
    if (_tuyaOp == 'toggle') {
      // Espelha o backend: inverte a partir do ultimo estado conhecido.
      final status = _tuyaDevice()?['status'] as Map? ?? const {};
      value = !(status[_tuyaCode] == true);
    } else if (type == 'Boolean') {
      value = _tuyaValue == 'true';
    } else if (type == 'Integer') {
      value = int.tryParse(_tuyaValue) ?? 0;
    } else {
      value = _tuyaValue;
    }

    try {
      await Injector.get<DroidDeckClient>()
          .sendTuyaCommand(_tuyaDeviceId!, _tuyaCode!, value);
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Comando enviado.')));
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text(e.toString().replaceFirst('Exception: ', ''))));
      }
    }
  }

  void _loadObsState() {
    Injector.get<DroidDeckClient>().getObsState().then((s) {
      if (s != null && mounted) {
        Injector.get<SignalRService>().obsState.value =
            Map<String, dynamic>.from(s);
      }
    }).catchError((_) {});
  }

  List<Widget> _buildObsFields() {
    final s = Injector.get<SignalRService>().obsState.value;
    final scenes =
        (s['scenes'] as List?)?.map((e) => '$e').toList() ?? <String>[];
    final inputs = (s['audioInputs'] as List?)
            ?.map((e) => '${(e is Map) ? e['name'] : e}')
            .toList() ??
        <String>[];

    final fields = <Widget>[
      _obsWarning(),
      DropdownButtonFormField<String>(
        value: _obsOp,
        isExpanded: true,
        decoration: const InputDecoration(
            labelText: 'Ação do OBS', border: OutlineInputBorder()),
        items: const [
          DropdownMenuItem(value: 'setScene', child: Text('Trocar de cena')),
          DropdownMenuItem(value: 'toggleRecord', child: Text('Gravar (alternar)')),
          DropdownMenuItem(
              value: 'toggleStream', child: Text('Transmitir / Live (alternar)')),
          DropdownMenuItem(
              value: 'toggleVirtualCam', child: Text('Câmera virtual (alternar)')),
          DropdownMenuItem(
              value: 'toggleReplayBuffer', child: Text('Replay buffer (alternar)')),
          DropdownMenuItem(value: 'saveReplay', child: Text('Salvar replay')),
          DropdownMenuItem(
              value: 'toggleInputMute', child: Text('Mutar fonte (alternar)')),
        ],
        onChanged: (v) {
          setState(() => _obsOp = v ?? 'setScene');
          _loadObsState();
        },
      ),
      const SizedBox(height: 12),
    ];

    if (_obsOp == 'setScene') {
      fields.add(DropdownButtonFormField<String>(
        value: scenes.contains(_obsScene) ? _obsScene : null,
        isExpanded: true,
        decoration: InputDecoration(
          labelText: 'Cena',
          border: const OutlineInputBorder(),
          helperText: scenes.isEmpty ? 'Conecte o OBS pra listar as cenas' : null,
        ),
        items: scenes
            .map((c) => DropdownMenuItem(
                value: c, child: Text(c, overflow: TextOverflow.ellipsis)))
            .toList(),
        onChanged: (v) => setState(() => _obsScene = v),
      ));
    } else if (_obsOp == 'toggleInputMute') {
      fields.add(DropdownButtonFormField<String>(
        value: inputs.contains(_obsInput) ? _obsInput : null,
        isExpanded: true,
        decoration: InputDecoration(
          labelText: 'Fonte de áudio',
          border: const OutlineInputBorder(),
          helperText: inputs.isEmpty ? 'Conecte o OBS pra listar as fontes' : null,
        ),
        items: inputs
            .map((c) => DropdownMenuItem(
                value: c, child: Text(c, overflow: TextOverflow.ellipsis)))
            .toList(),
        onChanged: (v) => setState(() => _obsInput = v),
      ));
    }

    fields.add(const Padding(
      padding: EdgeInsets.only(top: 6),
      child: Text('Requer o OBS aberto e conectado (config do DroidDeck).',
          style: TextStyle(fontSize: 11, color: Colors.grey)),
    ));
    return fields;
  }

  Widget _obsWarning() {
    return Watch((context) {
      final connected =
          Injector.get<SignalRService>().obsState.value['connected'] == true;
      if (connected) return const SizedBox.shrink();
      return Container(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: Colors.orange.withValues(alpha: 0.18),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: Colors.orange.withValues(alpha: 0.5)),
        ),
        child: Row(
          children: [
            const Icon(Icons.warning_amber_rounded, color: Colors.orange, size: 20),
            const SizedBox(width: 8),
            const Expanded(
                child: Text('OBS não conectado — esta ação não funciona até conectar.',
                    style: TextStyle(fontSize: 12))),
            TextButton(
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const ObsSettingsPage()),
              ),
              child: const Text('Configurar'),
            ),
          ],
        ),
      );
    });
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
