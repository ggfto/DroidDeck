import 'package:json_annotation/json_annotation.dart';
import 'mixer_master.dart';
import 'mixer_config.dart';

part 'mixer_entity.g.dart';

@JsonSerializable()
class MixerEntity {
  @JsonKey(name: 'device')
  final MixerMaster device;

  @JsonKey(name: 'config')
  final MixerConfig config;

  @JsonKey(name: 'id')
  final String? id;

  @JsonKey(name: 'volume', defaultValue: 0)
  final int volume;

  @JsonKey(name: 'mute', defaultValue: false)
  final bool mute;

  MixerEntity({
    required this.device,
    required this.config,
    this.id,
    required this.volume,
    required this.mute,
  });

  factory MixerEntity.fromJson(Map<String, dynamic> json) =>
      _$MixerEntityFromJson(json);
  Map<String, dynamic> toJson() => _$MixerEntityToJson(this);
}
