import 'package:json_annotation/json_annotation.dart';
import 'deck_button.dart';

part 'deck_profile.g.dart';

@JsonSerializable()
class DeckProfile {
  @JsonKey(name: 'id')
  final String id;

  @JsonKey(name: 'name')
  final String name;

  @JsonKey(name: 'rows')
  final int rows;

  @JsonKey(name: 'columns')
  final int columns;

  @JsonKey(name: 'isDefault')
  final bool isDefault;

  @JsonKey(name: 'buttons')
  final List<DeckButton> buttons;

  DeckProfile({
    required this.id,
    this.name = 'New Profile',
    this.rows = 3,
    this.columns = 4,
    this.isDefault = false,
    this.buttons = const [],
  });

  factory DeckProfile.fromJson(Map<String, dynamic> json) =>
      _$DeckProfileFromJson(json);

  Map<String, dynamic> toJson() => _$DeckProfileToJson(this);
}
