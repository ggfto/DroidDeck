import 'package:json_annotation/json_annotation.dart';

part 'mixer_data.g.dart';

@JsonSerializable()
class MixerData {
  @JsonKey(name: 'session')
  final int session;

  @JsonKey(name: 'volume')
  final int? volume;

  @JsonKey(name: 'mute')
  final bool? mute;

  MixerData({
    this.session = -1,
    this.volume,
    this.mute,
  });

  factory MixerData.fromJson(Map<String, dynamic> json) =>
      _$MixerDataFromJson(json);
  Map<String, dynamic> toJson() => _$MixerDataToJson(this);
}
