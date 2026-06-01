import 'package:json_annotation/json_annotation.dart';

part 'mixer_channel.g.dart';

@JsonSerializable()
class MixerChannel {
  @JsonKey(name: 'id')
  final int id;

  @JsonKey(name: 'description')
  final String? description;

  @JsonKey(name: 'volume', defaultValue: 0)
  final int volume;

  @JsonKey(name: 'icon')
  final String? icon;

  @JsonKey(name: 'mute', defaultValue: false)
  final bool mute;

  MixerChannel({
    required this.id,
    this.description,
    required this.volume,
    this.icon,
    required this.mute,
  });

  factory MixerChannel.fromJson(Map<String, dynamic> json) =>
      _$MixerChannelFromJson(json);
  Map<String, dynamic> toJson() => _$MixerChannelToJson(this);
}
