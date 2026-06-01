// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'mixer_channel.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MixerChannel _$MixerChannelFromJson(Map<String, dynamic> json) => MixerChannel(
      id: (json['id'] as num).toInt(),
      description: json['description'] as String?,
      volume: (json['volume'] as num?)?.toInt() ?? 0,
      icon: json['icon'] as String?,
      mute: json['mute'] as bool? ?? false,
    );

Map<String, dynamic> _$MixerChannelToJson(MixerChannel instance) =>
    <String, dynamic>{
      'id': instance.id,
      'description': instance.description,
      'volume': instance.volume,
      'icon': instance.icon,
      'mute': instance.mute,
    };
