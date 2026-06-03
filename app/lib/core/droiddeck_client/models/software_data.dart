import 'package:json_annotation/json_annotation.dart';

part 'software_data.g.dart';

@JsonSerializable()
class SoftwareData {
  @JsonKey(name: 'Name')
  final String? name;

  @JsonKey(name: 'Action')
  final String? action;

  SoftwareData({
    this.name,
    this.action,
  });

  factory SoftwareData.fromJson(Map<String, dynamic> json) => _$SoftwareDataFromJson(json);
  Map<String, dynamic> toJson() => _$SoftwareDataToJson(this);
}
