// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'mixer_entity.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MixerEntity _$MixerEntityFromJson(Map<String, dynamic> json) => MixerEntity(
      device: MixerMaster.fromJson(json['device'] as Map<String, dynamic>),
      config: MixerConfig.fromJson(json['config'] as Map<String, dynamic>),
      id: json['id'] as String?,
      volume: (json['volume'] as num?)?.toInt() ?? 0,
      mute: json['mute'] as bool? ?? false,
    );

Map<String, dynamic> _$MixerEntityToJson(MixerEntity instance) =>
    <String, dynamic>{
      'device': instance.device,
      'config': instance.config,
      'id': instance.id,
      'volume': instance.volume,
      'mute': instance.mute,
    };
