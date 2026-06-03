// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'deck_button.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DeckButton _$DeckButtonFromJson(Map<String, dynamic> json) => DeckButton(
  id: json['id'] as String,
  row: (json['row'] as num).toInt(),
  column: (json['column'] as num).toInt(),
  label: json['label'] as String?,
  iconBase64: json['iconBase64'] as String?,
  iconName: json['iconName'] as String?,
  backgroundColor: json['backgroundColor'] as String?,
  activeColor: json['activeColor'] as String?,
  action: json['action'] == null
      ? null
      : DeckAction.fromJson(json['action'] as Map<String, dynamic>),
  dynamicType: json['dynamicType'] as String?,
);

Map<String, dynamic> _$DeckButtonToJson(DeckButton instance) =>
    <String, dynamic>{
      'id': instance.id,
      'row': instance.row,
      'column': instance.column,
      'label': instance.label,
      'iconBase64': instance.iconBase64,
      'iconName': instance.iconName,
      'backgroundColor': instance.backgroundColor,
      'activeColor': instance.activeColor,
      'action': instance.action,
      'dynamicType': instance.dynamicType,
    };
