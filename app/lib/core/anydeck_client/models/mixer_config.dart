import 'package:json_annotation/json_annotation.dart';

part 'mixer_config.g.dart';

@JsonSerializable()
class MixerConfig {
  @JsonKey(name: 'enabled')
  final bool? enabled;

  @JsonKey(name: 'customName')
  final String? customName;

  MixerConfig({
    this.enabled,
    this.customName,
  });

  factory MixerConfig.fromJson(Map<String, dynamic> json) =>
      _$MixerConfigFromJson(json);
  Map<String, dynamic> toJson() => _$MixerConfigToJson(this);
}
