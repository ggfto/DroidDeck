import 'package:json_annotation/json_annotation.dart';

part 'audio_target.g.dart';

@JsonSerializable()
class AudioTarget {
  @JsonKey(name: 'ProcessName')
  final String? processName;

  @JsonKey(name: 'ProcessId')
  final int? processId;

  @JsonKey(name: 'Mute')
  final bool? mute;

  AudioTarget({
    this.processName,
    this.processId,
    this.mute,
  });

  factory AudioTarget.fromJson(Map<String, dynamic> json) => _$AudioTargetFromJson(json);
  Map<String, dynamic> toJson() => _$AudioTargetToJson(this);
}
