import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

class ButtonPropertyPanel extends StatefulWidget {
  final DeckButton button;
  final Function(DeckButton) onSave;
  final VoidCallback? onCancel;

  const ButtonPropertyPanel({
    super.key,
    required this.button,
    required this.onSave,
    this.onCancel,
  });

  @override
  State<ButtonPropertyPanel> createState() => _ButtonPropertyPanelState();
}

class _ButtonPropertyPanelState extends State<ButtonPropertyPanel> {
  late TextEditingController _labelController;
  late TextEditingController _actionParamController;
  String _selectedActionType = 'none';
  String _mixerOperation = 'toggleMute';
  Color _selectedColor = Colors.grey[800]!;

  final List<String> _actionTypes = ['none', 'hotkey', 'launch_app', 'mixer'];
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
    }

    final newAction = _selectedActionType == 'none'
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
                  ],
                  onChanged: (val) {
                    setState(() {
                      _dynamicType = val;
                      // If dynamic type is selected, we might want to auto-set label/icon?
                      if (val != null) {
                        // Optional: Clear label/icon to show dynamic content clearly
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
          color: isSelected ? Colors.blue.withOpacity(0.3) : Colors.grey[800],
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
