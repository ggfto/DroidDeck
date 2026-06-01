// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'mixer_master.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MixerMaster _$MixerMasterFromJson(Map<String, dynamic> json) => MixerMaster(
      id: json['id'] as String?,
      title: json['title'] as String?,
      description: json['description'] as String?,
      volume: (json['volume'] as num?)?.toInt() ?? 0,
      icon: json['icon'] as String?,
      mute: json['mute'] as bool? ?? false,
      channels: (json['channels'] as List<dynamic>?)
          ?.map((e) => MixerChannel.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

Map<String, dynamic> _$MixerMasterToJson(MixerMaster instance) =>
    <String, dynamic>{
      'id': instance.id,
      'title': instance.title,
      'description': instance.description,
      'volume': instance.volume,
      'icon': instance.icon,
      'mute': instance.mute,
      'channels': instance.channels,
    };
