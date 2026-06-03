import 'package:json_annotation/json_annotation.dart';

part 'deck_action.g.dart';

@JsonSerializable()
class DeckAction {
  @JsonKey(name: 'type')
  final String type;

  @JsonKey(name: 'parameters')
  final Map<String, String> parameters;

  DeckAction({
    this.type = 'none',
    this.parameters = const {},
  });

  factory DeckAction.fromJson(Map<String, dynamic> json) =>
      _$DeckActionFromJson(json);

  Map<String, dynamic> toJson() => _$DeckActionToJson(this);
}
