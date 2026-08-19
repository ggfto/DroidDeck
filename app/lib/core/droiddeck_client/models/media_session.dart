import 'package:json_annotation/json_annotation.dart';

part 'media_session.g.dart';

@JsonSerializable()
class MediaSession {
  @JsonKey(name: 'id')
  final String? id;

  @JsonKey(name: 'title')
  final String? title;

  @JsonKey(name: 'artist')
  final String? artist;

  @JsonKey(name: 'albumTitle')
  final String? albumTitle;

  @JsonKey(name: 'thumbnailBase64')
  final String? thumbnailBase64;

  @JsonKey(name: 'playbackStatus')
  final String? playbackStatus;

  @JsonKey(name: 'canPlayPause')
  final bool canPlayPause;

  @JsonKey(name: 'canGoNext')
  final bool canGoNext;

  @JsonKey(name: 'canGoPrevious')
  final bool canGoPrevious;

  @JsonKey(name: 'position')
  final double position;

  @JsonKey(name: 'duration')
  final double duration;

  MediaSession({
    this.id,
    this.title,
    this.artist,
    this.albumTitle,
    this.thumbnailBase64,
    this.playbackStatus,
    this.canPlayPause = false,
    this.canGoNext = false,
    this.canGoPrevious = false,
    this.position = 0.0,
    this.duration = 0.0,
  });

  factory MediaSession.fromJson(Map<String, dynamic> json) =>
      _$MediaSessionFromJson(json);

  Map<String, dynamic> toJson() => _$MediaSessionToJson(this);

  /// Copia trocando so o que foi passado. Usado para aplicar o estado que o
  /// backend devolve junto do comando, sem esperar o proximo fetch.
  MediaSession copyWith({
    String? id,
    String? title,
    String? artist,
    String? albumTitle,
    String? thumbnailBase64,
    String? playbackStatus,
    bool? canPlayPause,
    bool? canGoNext,
    bool? canGoPrevious,
    double? position,
    double? duration,
  }) =>
      MediaSession(
        id: id ?? this.id,
        title: title ?? this.title,
        artist: artist ?? this.artist,
        albumTitle: albumTitle ?? this.albumTitle,
        thumbnailBase64: thumbnailBase64 ?? this.thumbnailBase64,
        playbackStatus: playbackStatus ?? this.playbackStatus,
        canPlayPause: canPlayPause ?? this.canPlayPause,
        canGoNext: canGoNext ?? this.canGoNext,
        canGoPrevious: canGoPrevious ?? this.canGoPrevious,
        position: position ?? this.position,
        duration: duration ?? this.duration,
      );

  bool get isPlaying => playbackStatus?.toLowerCase() == 'playing';
  bool get isPaused => playbackStatus?.toLowerCase() == 'paused';
}

@JsonSerializable()
class MediaCommandData {
  @JsonKey(name: 'command')
  final String command;

  MediaCommandData({required this.command});

  factory MediaCommandData.fromJson(Map<String, dynamic> json) =>
      _$MediaCommandDataFromJson(json);

  Map<String, dynamic> toJson() => _$MediaCommandDataToJson(this);
}
