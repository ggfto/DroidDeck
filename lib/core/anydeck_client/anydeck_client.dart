import 'package:dio/dio.dart';
import 'models/mixer_data.dart';
import 'models/software_data.dart';
import 'models/audio_target.dart';
import 'models/mixer_entity.dart';
import 'models/mixer_master.dart';
import 'models/media_session.dart';
import 'models/deck_profile.dart';
import 'models/deck_action.dart';

class AnyDeckClient {
  final Dio _dio;
  final String baseUrl;

  AnyDeckClient({
    required String baseUrl,
    Dio? dio,
  })  : _dio = dio ?? Dio(),
        baseUrl = baseUrl {
    _dio.options.baseUrl = baseUrl;
  }

  void setBaseUrl(String url) {
    _dio.options.baseUrl = url;
  }

  /// Define (ou remove) a chave de API enviada em todas as requisições (header X-API-KEY).
  void setApiKey(String? key) {
    if (key == null || key.isEmpty) {
      _dio.options.headers.remove('X-API-KEY');
    } else {
      _dio.options.headers['X-API-KEY'] = key;
    }
  }

  Future<List<MixerEntity>> getOutputs() async {
    final response = await _dio.get('/api/v1/Mixer/out');
    return (response.data as List)
        .map((e) => MixerEntity.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<MixerEntity> getOutput(String id) async {
    final response = await _dio.get('/api/v1/Mixer/out/$id');
    return MixerEntity.fromJson(response.data as Map<String, dynamic>);
  }

  Future<MixerMaster> setOutput(String id, MixerData data) async {
    final response = await _dio.put(
      '/api/v1/Mixer/out/$id',
      data: data.toJson(),
    );
    return MixerMaster.fromJson(response.data as Map<String, dynamic>);
  }

  Future<List<MixerEntity>> getInputs() async {
    final response = await _dio.get('/api/v1/Mixer/in');
    return (response.data as List)
        .map((e) => MixerEntity.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<MixerEntity> getInput(String id) async {
    final response = await _dio.get('/api/v1/Mixer/in/$id');
    return MixerEntity.fromJson(response.data as Map<String, dynamic>);
  }

  Future<MixerMaster> setInput(String id, MixerData data) async {
    final response = await _dio.put(
      '/api/v1/Mixer/in/$id',
      data: data.toJson(),
    );
    return MixerMaster.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> activate(SoftwareData data) async {
    await _dio.post(
      '/api/Software/activate',
      data: data.toJson(),
    );
  }

  Future<Map<String, dynamic>> mute(AudioTarget target) async {
    final response = await _dio.post(
      '/api/Software/mute',
      data: target.toJson(),
    );
    return response.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> toggleMute(AudioTarget target) async {
    final response = await _dio.post(
      '/api/Software/toggle-mute',
      data: target.toJson(),
    );
    return response.data as Map<String, dynamic>;
  }

  /// Estado de mute atual de um processo (null se não houver sessão de áudio).
  Future<bool?> getMuteState(String process) async {
    final response = await _dio.get('/api/Software/mute-state/$process');
    final data = response.data;
    if (data is Map && data['muted'] is bool) return data['muted'] as bool;
    return null;
  }

  /// Estado atual do Discord (connected/mute/deaf/channelId/channelName/
  /// inputVolume/outputVolume/voiceMode/participants) ou null se indisponível.
  Future<Map<String, dynamic>?> getDiscordState() async {
    try {
      final response = await _dio.get('/api/discord/state');
      return (response.data as Map).cast<String, dynamic>();
    } catch (_) {
      return null;
    }
  }

  /// Servidores do Discord do usuário ([{id, name}]).
  Future<List<Map<String, String>>> getDiscordGuilds() async {
    try {
      final r = await _dio.get('/api/discord/guilds');
      return (r.data as List)
          .map<Map<String, String>>(
              (e) => {'id': '${e['id']}', 'name': '${e['name'] ?? ''}'})
          .toList();
    } catch (_) {
      return [];
    }
  }

  /// Canais de voz de um servidor ([{id, name}]).
  Future<List<Map<String, String>>> getDiscordChannels(String guildId) async {
    try {
      final r = await _dio.get('/api/discord/channels/$guildId');
      return (r.data as List)
          .map<Map<String, String>>(
              (e) => {'id': '${e['id']}', 'name': '${e['name'] ?? ''}'})
          .toList();
    } catch (_) {
      return [];
    }
  }

  // Media Control methods
  Future<List<MediaSession>> getMediaSessions() async {
    final response = await _dio.get('/api/v1/Media/sessions');
    return (response.data as List)
        .map((e) => MediaSession.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<MediaSession> getMediaSession(String id) async {
    final response = await _dio.get('/api/v1/Media/sessions/$id');
    return MediaSession.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> sendMediaCommand(String id, String command) async {
    await _dio.post(
      '/api/v1/Media/sessions/$id/command',
      data: {'command': command},
    );
  }

  // StreamDeck methods
  Future<List<DeckProfile>> getProfiles() async {
    final response = await _dio.get('/api/StreamDeck/profiles');
    return (response.data as List)
        .map((e) => DeckProfile.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<DeckProfile> getProfile(String id) async {
    final response = await _dio.get('/api/StreamDeck/profiles/$id');
    return DeckProfile.fromJson(response.data as Map<String, dynamic>);
  }

  Future<void> saveProfile(DeckProfile profile) async {
    await _dio.post(
      '/api/StreamDeck/profiles',
      data: profile.toJson(),
    );
  }

  Future<void> deleteProfile(String id) async {
    await _dio.delete('/api/StreamDeck/profiles/$id');
  }

  Future<void> executeAction(DeckAction action) async {
    await _dio.post(
      '/api/StreamDeck/execute',
      data: action.toJson(),
    );
  }

  /// Grade física do deck persistida no PC ({rows, columns}).
  Future<Map<String, int>?> getLayout() async {
    try {
      final r = await _dio.get('/api/StreamDeck/layout');
      final m = (r.data as Map);
      return {
        'rows': (m['rows'] as num).toInt(),
        'columns': (m['columns'] as num).toInt(),
      };
    } catch (_) {
      return null;
    }
  }

  /// O celular reporta quantos botões cabem inteiros na tela.
  Future<void> saveLayout(int rows, int columns) async {
    await _dio.post(
      '/api/StreamDeck/layout',
      data: {'rows': rows, 'columns': columns},
    );
  }
}
