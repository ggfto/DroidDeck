import 'package:json_annotation/json_annotation.dart';
import 'deck_action.dart';

part 'deck_button.g.dart';

@JsonSerializable()
class DeckButton {
  @JsonKey(name: 'id')
  final String id;

  @JsonKey(name: 'row')
  final int row;

  @JsonKey(name: 'column')
  final int column;

  @JsonKey(name: 'label')
  final String? label;

  @JsonKey(name: 'iconBase64')
  final String? iconBase64;

  @JsonKey(name: 'iconName')
  final String? iconName;

  @JsonKey(name: 'backgroundColor')
  final String? backgroundColor;

  /// Cor exibida quando o botão está "ativo" (toggle ligado: mute/discord).
  /// Se nulo, o runtime usa vermelho como padrão.
  @JsonKey(name: 'activeColor')
  final String? activeColor;

  @JsonKey(name: 'action')
  final DeckAction? action;

  @JsonKey(name: 'dynamicType')
  final String? dynamicType;

  DeckButton({
    required this.id,
    required this.row,
    required this.column,
    this.label,
    this.iconBase64,
    this.iconName,
    this.backgroundColor,
    this.activeColor,
    this.action,
    this.dynamicType,
  });

  factory DeckButton.fromJson(Map<String, dynamic> json) =>
      _$DeckButtonFromJson(json);

  Map<String, dynamic> toJson() => _$DeckButtonToJson(this);
}
