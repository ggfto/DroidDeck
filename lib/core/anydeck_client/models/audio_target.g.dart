// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'audio_target.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AudioTarget _$AudioTargetFromJson(Map<String, dynamic> json) => AudioTarget(
      processName: json['ProcessName'] as String?,
      processId: (json['ProcessId'] as num?)?.toInt(),
      mute: json['Mute'] as bool?,
    );

Map<String, dynamic> _$AudioTargetToJson(AudioTarget instance) =>
    <String, dynamic>{
      'ProcessName': instance.processName,
      'ProcessId': instance.processId,
      'Mute': instance.mute,
    };
