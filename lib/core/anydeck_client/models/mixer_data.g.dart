// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'mixer_data.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MixerData _$MixerDataFromJson(Map<String, dynamic> json) => MixerData(
      session: (json['session'] as num?)?.toInt() ?? -1,
      volume: (json['volume'] as num?)?.toInt(),
      mute: json['mute'] as bool?,
    );

Map<String, dynamic> _$MixerDataToJson(MixerData instance) => <String, dynamic>{
      'session': instance.session,
      'volume': instance.volume,
      'mute': instance.mute,
    };
