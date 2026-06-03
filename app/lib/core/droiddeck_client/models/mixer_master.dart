import 'package:json_annotation/json_annotation.dart';
import 'mixer_channel.dart';

part 'mixer_master.g.dart';

@JsonSerializable()
class MixerMaster {
  @JsonKey(name: 'id')
  final String? id;

  @JsonKey(name: 'title')
  final String? title;

  @JsonKey(name: 'description')
  final String? description;

  @JsonKey(name: 'volume', defaultValue: 0)
  final int volume;

  @JsonKey(name: 'icon')
  final String? icon;

  @JsonKey(name: 'mute', defaultValue: false)
  final bool mute;

  @JsonKey(name: 'channels')
  final List<MixerChannel>? channels;

  MixerMaster({
    this.id,
    this.title,
    this.description,
    required this.volume,
    this.icon,
    required this.mute,
    this.channels,
  });

  factory MixerMaster.fromJson(Map<String, dynamic> json) =>
      _$MixerMasterFromJson(json);
  Map<String, dynamic> toJson() => _$MixerMasterToJson(this);
}
