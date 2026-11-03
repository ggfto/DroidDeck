import 'package:flutter/material.dart';

import '../buttons/button_icons.dart';

/// Descrição de um tipo de ação, vinda de GET /api/actions/schema. O editor monta o
/// formulário a partir dos campos; tipos com [editor] usam uma tela própria.
class ActionSchema {
  final String type;
  final String title;
  final String icon;
  final String category;
  final String? description;
  final bool privileged;
  final bool stateful;
  final String? editor;
  final String? settings;
  final String? stateEvent;
  final List<ActionField> fields;

  const ActionSchema({
    required this.type,
    required this.title,
    this.icon = 'extension',
    this.category = 'Utilidades',
    this.description,
    this.privileged = false,
    this.stateful = false,
    this.editor,
    this.settings,
    this.stateEvent,
    this.fields = const [],
  });

  factory ActionSchema.fromJson(Map<String, dynamic> j) => ActionSchema(
        type: j['type'] as String? ?? '',
        title: j['title'] as String? ?? (j['type'] as String? ?? ''),
        icon: j['icon'] as String? ?? 'extension',
        category: j['category'] as String? ?? 'Utilidades',
        description: j['description'] as String?,
        privileged: j['privileged'] == true,
        stateful: j['stateful'] == true,
        editor: j['editor'] as String?,
        settings: j['settings'] as String?,
        stateEvent: j['stateEvent'] as String?,
        fields: (j['fields'] as List? ?? const [])
            .map((f) => ActionField.fromJson(Map<String, dynamic>.from(f as Map)))
            .toList(),
      );

  IconData get iconData => actionIcon(icon);

  /// Valores iniciais de um botão novo deste tipo.
  Map<String, String> defaults() => {
        for (final f in fields)
          if (f.defaultValue != null) f.key: f.defaultValue!,
      };
}

class ActionField {
  final String key;
  final String label;
  final String kind;
  final String? defaultValue;
  final String? help;
  final String? hint;
  final bool required;
  final List<FieldOption> options;
  final String? optionsSource;
  final bool allowCustom;
  final double? min;
  final double? max;
  final String? unit;
  final Map<String, List<String>> visibleWhen;

  const ActionField({
    required this.key,
    required this.label,
    this.kind = 'text',
    this.defaultValue,
    this.help,
    this.hint,
    this.required = false,
    this.options = const [],
    this.optionsSource,
    this.allowCustom = false,
    this.min,
    this.max,
    this.unit,
    this.visibleWhen = const {},
  });

  factory ActionField.fromJson(Map<String, dynamic> j) => ActionField(
        key: j['key'] as String? ?? '',
        label: j['label'] as String? ?? '',
        kind: j['kind'] as String? ?? 'text',
        defaultValue: j['default'] as String?,
        help: j['help'] as String?,
        hint: j['hint'] as String?,
        required: j['required'] == true,
        options: (j['options'] as List? ?? const [])
            .map((o) => FieldOption.fromJson(o))
            .toList(),
        optionsSource: j['optionsSource'] as String?,
        allowCustom: j['allowCustom'] == true,
        min: (j['min'] as num?)?.toDouble(),
        max: (j['max'] as num?)?.toDouble(),
        unit: j['unit'] as String?,
        visibleWhen: (j['visibleWhen'] as Map? ?? const {}).map(
          (k, v) => MapEntry(
              k as String, (v as List).map((e) => '$e').toList()),
        ),
      );

  /// O campo aparece com os valores atuais do formulário?
  bool isVisible(Map<String, String> values) => visibleWhen.entries.every(
      (e) => e.value.contains(values[e.key] ?? ''));
}

class FieldOption {
  final String value;
  final String label;

  const FieldOption(this.value, this.label);

  /// Aceita string, {value, label} ou {name} (o formato das listas do OBS).
  factory FieldOption.fromJson(dynamic o) {
    if (o is Map) {
      final value = '${o['value'] ?? o['id'] ?? o['name'] ?? ''}';
      return FieldOption(value, '${o['label'] ?? o['name'] ?? value}');
    }
    return FieldOption('$o', '$o');
  }
}

/// Ícone de um tipo de ação pelo nome que o PC manda. Nome desconhecido vira genérico.
IconData actionIcon(String name) => buttonIconData(name);

/// Catálogo usado quando o PC ainda roda uma versão sem /api/actions/schema.
/// Espelha o que esses servidores aceitam.
const fallbackActionCatalog = <ActionSchema>[
  ActionSchema(
    type: 'hotkey',
    title: 'Atalho de teclado',
    icon: 'keyboard',
    category: 'Sistema',
    privileged: true,
    fields: [
      ActionField(
          key: 'keys',
          label: 'Teclas',
          hint: '^C',
          help: '^ = Ctrl, + = Shift, % = Alt, # = Win, {ENTER}, {F5}…',
          required: true),
    ],
  ),
  ActionSchema(
    type: 'launch_app',
    title: 'Abrir programa',
    icon: 'launch',
    category: 'Sistema',
    privileged: true,
    fields: [
      ActionField(
          key: 'path',
          label: 'Caminho',
          hint: r'C:\Windows\System32\notepad.exe',
          required: true),
    ],
  ),
  ActionSchema(
    type: 'media',
    title: 'Mídia',
    icon: 'play',
    category: 'Mídia',
    fields: [
      ActionField(
        key: 'command',
        label: 'Comando',
        kind: 'select',
        defaultValue: 'playpause',
        options: [
          FieldOption('playpause', 'Play / Pause'),
          FieldOption('next', 'Próxima faixa'),
          FieldOption('previous', 'Faixa anterior'),
          FieldOption('stop', 'Parar'),
          FieldOption('play', 'Play'),
          FieldOption('pause', 'Pause'),
        ],
      ),
    ],
  ),
  ActionSchema(
      type: 'mixer',
      title: 'Volume e mudo',
      icon: 'volume_up',
      category: 'Mídia',
      stateful: true,
      editor: 'mixer'),
  ActionSchema(
      type: 'multi',
      title: 'Multi-ação',
      icon: 'playlist_play',
      category: 'Utilidades',
      editor: 'multi'),
  ActionSchema(
      type: 'discord',
      title: 'Discord',
      icon: 'headset_mic',
      category: 'Streaming',
      stateful: true,
      editor: 'discord',
      settings: 'discord'),
  ActionSchema(
    type: 'obs',
    title: 'OBS Studio',
    icon: 'videocam',
    category: 'Streaming',
    stateful: true,
    settings: 'obs',
    stateEvent: 'ReceiveObsState',
    fields: [
      ActionField(
        key: 'operation',
        label: 'Ação do OBS',
        kind: 'select',
        defaultValue: 'setScene',
        options: [
          FieldOption('setScene', 'Trocar de cena'),
          FieldOption('toggleRecord', 'Gravar (alternar)'),
          FieldOption('toggleStream', 'Transmitir / Live (alternar)'),
          FieldOption('toggleVirtualCam', 'Câmera virtual (alternar)'),
          FieldOption('toggleReplayBuffer', 'Replay buffer (alternar)'),
          FieldOption('saveReplay', 'Salvar replay'),
          FieldOption('toggleInputMute', 'Mutar fonte de áudio (alternar)'),
        ],
      ),
      ActionField(
        key: 'scene',
        label: 'Cena',
        kind: 'select',
        optionsSource: '/api/obs/scenes',
        required: true,
        visibleWhen: {
          'operation': ['setScene']
        },
      ),
      ActionField(
        key: 'inputName',
        label: 'Fonte de áudio',
        kind: 'select',
        optionsSource: '/api/obs/audio-inputs',
        required: true,
        visibleWhen: {
          'operation': ['toggleInputMute']
        },
      ),
    ],
  ),
  ActionSchema(
      type: 'soundboard',
      title: 'Soundboard',
      icon: 'campaign',
      category: 'Streaming',
      editor: 'soundboard'),
  ActionSchema(
      type: 'tuya',
      title: 'Casa inteligente (Tuya)',
      icon: 'lightbulb',
      category: 'Casa',
      stateful: true,
      editor: 'tuya',
      settings: 'tuya'),
  ActionSchema(
      type: 'open_profile',
      title: 'Abrir perfil',
      icon: 'folder',
      category: 'Navegação',
      editor: 'open_profile'),
  ActionSchema(
      type: 'back',
      title: 'Voltar',
      icon: 'arrow_back',
      category: 'Navegação',
      description: 'Volta ao perfil anterior.'),
];
