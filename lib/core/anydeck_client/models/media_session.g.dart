// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'media_session.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MediaSession _$MediaSessionFromJson(Map<String, dynamic> json) => MediaSession(
  id: json['id'] as String?,
  title: json['title'] as String?,
  artist: json['artist'] as String?,
  albumTitle: json['albumTitle'] as String?,
  thumbnailBase64: json['thumbnailBase64'] as String?,
  playbackStatus: json['playbackStatus'] as String?,
  canPlayPause: json['canPlayPause'] as bool? ?? false,
  canGoNext: json['canGoNext'] as bool? ?? false,
  canGoPrevious: json['canGoPrevious'] as bool? ?? false,
  position: (json['position'] as num?)?.toDouble() ?? 0.0,
  duration: (json['duration'] as num?)?.toDouble() ?? 0.0,
);

Map<String, dynamic> _$MediaSessionToJson(MediaSession instance) =>
    <String, dynamic>{
      'id': instance.id,
      'title': instance.title,
      'artist': instance.artist,
      'albumTitle': instance.albumTitle,
      'thumbnailBase64': instance.thumbnailBase64,
      'playbackStatus': instance.playbackStatus,
      'canPlayPause': instance.canPlayPause,
      'canGoNext': instance.canGoNext,
      'canGoPrevious': instance.canGoPrevious,
      'position': instance.position,
      'duration': instance.duration,
    };

MediaCommandData _$MediaCommandDataFromJson(Map<String, dynamic> json) =>
    MediaCommandData(command: json['command'] as String);

Map<String, dynamic> _$MediaCommandDataToJson(MediaCommandData instance) =>
    <String, dynamic>{'command': instance.command};
