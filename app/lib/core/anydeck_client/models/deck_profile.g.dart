// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'deck_profile.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DeckProfile _$DeckProfileFromJson(Map<String, dynamic> json) => DeckProfile(
  id: json['id'] as String,
  name: json['name'] as String? ?? 'New Profile',
  rows: (json['rows'] as num?)?.toInt() ?? 3,
  columns: (json['columns'] as num?)?.toInt() ?? 4,
  isDefault: json['isDefault'] as bool? ?? false,
  buttons:
      (json['buttons'] as List<dynamic>?)
          ?.map((e) => DeckButton.fromJson(e as Map<String, dynamic>))
          .toList() ??
      const [],
);

Map<String, dynamic> _$DeckProfileToJson(DeckProfile instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'rows': instance.rows,
      'columns': instance.columns,
      'isDefault': instance.isDefault,
      'buttons': instance.buttons,
    };
