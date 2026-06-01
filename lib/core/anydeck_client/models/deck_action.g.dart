// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'deck_action.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DeckAction _$DeckActionFromJson(Map<String, dynamic> json) => DeckAction(
      type: json['type'] as String? ?? 'none',
      parameters: (json['parameters'] as Map<String, dynamic>?)?.map(
            (k, e) => MapEntry(k, e as String),
          ) ??
          const {},
    );

Map<String, dynamic> _$DeckActionToJson(DeckAction instance) =>
    <String, dynamic>{
      'type': instance.type,
      'parameters': instance.parameters,
    };
