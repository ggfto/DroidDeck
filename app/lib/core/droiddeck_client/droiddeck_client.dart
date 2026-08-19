import 'package:dio/dio.dart';
import 'models/mixer_data.dart';
import 'models/software_data.dart';
import 'models/audio_target.dart';
import 'models/mixer_entity.dart';
import 'models/mixer_master.dart';
import 'models/media_session.dart';
import 'models/deck_profile.dart';
import 'models/deck_action.dart';
import 'models/sound_result.dart';

class DroidDeckClient {
  final Dio _dio;
  final String baseUrl;

  DroidDeckClient({
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

  /// Salva as credenciais do app Discord (Client ID + Secret) no PC.
  Future<void> setDiscordConfig(String clientId, String clientSecret) async {
    await _dio.post('/api/discord/config',
        data: {'clientId': clientId, 'clientSecret': clientSecret});
  }

  /// Conecta ao Discord (abre o popup de autorização no PC na 1ª vez).
  /// Lança [Exception] com mensagem legível em caso de erro.
  Future<Map<String, dynamic>> connectDiscord() async {
    try {
      final r = await _dio.post('/api/discord/connect');
      return (r.data as Map).cast<String, dynamic>();
    } on DioException catch (e) {
      final data = e.response?.data;
      final msg = (data is Map && data['error'] != null)
          ? '${data['error']}'
          : 'Falha ao conectar — o Discord está aberto no PC?';
      throw Exception(msg);
    }
  }

  /// Sons da soundboard NATIVA do Discord ([{soundId, name, guildId, emojiName}]).
  Future<List<Map<String, String>>> getDiscordSoundboardSounds() async {
    try {
      final r = await _dio.get('/api/discord/soundboard-sounds');
      return (r.data as List)
          .map<Map<String, String>>((e) => {
                'soundId': '${e['soundId'] ?? ''}',
                'name': '${e['name'] ?? ''}',
                'guildId': '${e['guildId'] ?? ''}',
                'emojiName': '${e['emojiName'] ?? ''}',
              })
          .toList();
    } catch (_) {
      return [];
    }
  }

  /// Toca um som da soundboard nativa do Discord no canal de voz atual.
  Future<void> playDiscordSoundboard(String soundId, String? guildId) async {
    await _dio.post('/api/discord/play-soundboard',
        data: {'soundId': soundId, 'guildId': guildId});
  }

  // ---- OBS ----
  /// Estado do OBS (connected/currentScene/recording/streaming/virtualCam/
  /// replayBuffer/scenes/audioInputs) ou null se indisponível.
  Future<Map<String, dynamic>?> getObsState() async {
    try {
      final r = await _dio.get('/api/obs/state');
      return (r.data as Map).cast<String, dynamic>();
    } catch (_) {
      return null;
    }
  }

  /// Salva a config do OBS (host/porta/senha do obs-websocket).
  Future<void> setObsConfig(String host, int port, String? password) async {
    await _dio.post('/api/obs/config',
        data: {'host': host, 'port': port, 'password': password});
  }

  /// Conecta no OBS (obs-websocket). Lança [Exception] legível em caso de erro.
  Future<Map<String, dynamic>> connectObs() async {
    try {
      final r = await _dio.post('/api/obs/connect');
      return (r.data as Map).cast<String, dynamic>();
    } on DioException catch (e) {
      final data = e.response?.data;
      final msg = (data is Map && data['error'] != null)
          ? '${data['error']}'
          : 'Falha ao conectar no OBS (obs-websocket ativo?)';
      throw Exception(msg);
    }
  }

  // ---- Tuya / Smart Life ----
  /// Estado do plugin: paired/connected/push + os dispositivos com status e funções.
  Future<Map<String, dynamic>?> getTuyaState() async {
    try {
      final r = await _dio.get('/api/tuya/state');
      return (r.data as Map).cast<String, dynamic>();
    } catch (_) {
      return null;
    }
  }

  /// Passo 1 do pareamento: devolve `qrPayload` para renderizar o QR na tela.
  Future<Map<String, dynamic>> startTuyaPairing(String userCode) async {
    try {
      final r = await _dio
          .post('/api/tuya/pair/start', data: {'userCode': userCode});
      return (r.data as Map).cast<String, dynamic>();
    } on DioException catch (e) {
      throw Exception(_tuyaError(e, 'Falha ao gerar o QR de pareamento'));
    }
  }

  /// Passo 2: chamar em laço. `false` = ainda não escaneado (não é erro).
  /// Lança [Exception] quando o QR expirou (HTTP 410) e outro precisa ser gerado.
  Future<bool> pollTuyaPairing() async {
    try {
      final r = await _dio.post('/api/tuya/pair/poll');
      return (r.data as Map)['scanned'] == true;
    } on DioException catch (e) {
      throw Exception(_tuyaError(e, 'Falha ao verificar o pareamento'));
    }
  }

  /// Reenumera os dispositivos na nuvem. Caro em cota — só a pedido do usuário.
  Future<Map<String, dynamic>> refreshTuyaDevices() async {
    try {
      final r = await _dio.post('/api/tuya/devices/refresh');
      return (r.data as Map).cast<String, dynamic>();
    } on DioException catch (e) {
      throw Exception(_tuyaError(e, 'Falha ao atualizar os dispositivos'));
    }
  }

  /// Dispara um comando avulso (usado pelo botão "testar" do editor).
  Future<void> sendTuyaCommand(
      String deviceId, String code, Object? value) async {
    try {
      await _dio.post('/api/tuya/command',
          data: {'deviceId': deviceId, 'code': code, 'value': value});
    } on DioException catch (e) {
      throw Exception(_tuyaError(e, 'Falha ao enviar o comando'));
    }
  }

  static String _tuyaError(DioException e, String fallback) {
    final data = e.response?.data;
    return (data is Map && data['error'] != null) ? '${data['error']}' : fallback;
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

  /// Envia um comando de transporte e devolve o que o backend aplicou:
  /// `sessionId` (pode diferir do pedido, quando o id salvo nao existe mais) e
  /// `playing` (estado logo apos o comando). Mapa vazio se a resposta vier sem corpo.
  Future<Map<String, dynamic>> sendMediaCommand(String id, String command) async {
    final response = await _dio.post(
      '/api/v1/Media/sessions/$id/command',
      data: {'command': command},
    );
    final data = response.data;
    return data is Map<String, dynamic> ? data : <String, dynamic>{};
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

  // ---- Soundboard (MyInstants) ----
  /// Busca sons no MyInstants (proxiado pelo backend).
  Future<List<SoundResult>> searchSounds(String query) async {
    try {
      final r = await _dio.get('/api/Soundboard/search',
          queryParameters: {'q': query});
      return (r.data as List)
          .map((e) => SoundResult.fromJson((e as Map).cast<String, dynamic>()))
          .toList();
    } catch (_) {
      return [];
    }
  }

  /// Sons em alta no MyInstants.
  Future<List<SoundResult>> getTrendingSounds() async {
    try {
      final r = await _dio.get('/api/Soundboard/trending');
      return (r.data as List)
          .map((e) => SoundResult.fromJson((e as Map).cast<String, dynamic>()))
          .toList();
    } catch (_) {
      return [];
    }
  }

  /// Toca um som no PC (sai pelo dispositivo configurado — cabo/monitor).
  Future<void> playSound(String id, String url, String title) async {
    await _dio.post('/api/Soundboard/play',
        data: {'id': id, 'url': url, 'title': title});
  }

  /// Para tudo que estiver tocando na soundboard.
  Future<void> stopSounds() async {
    await _dio.post('/api/Soundboard/stop');
  }

  /// Dispositivos de saída de áudio disponíveis ([{id, name}]).
  Future<List<Map<String, String>>> getSoundDevices() async {
    try {
      final r = await _dio.get('/api/Soundboard/devices');
      return (r.data as List)
          .map<Map<String, String>>(
              (e) => {'id': '${e['id']}', 'name': '${e['name'] ?? ''}'})
          .toList();
    } catch (_) {
      return [];
    }
  }

  /// Config da soundboard ({cableDeviceId, monitorDeviceId, monitorEnabled, volume}).
  Future<Map<String, dynamic>?> getSoundboardConfig() async {
    try {
      final r = await _dio.get('/api/Soundboard/config');
      return (r.data as Map).cast<String, dynamic>();
    } catch (_) {
      return null;
    }
  }

  /// Salva a config da soundboard (seleção de dispositivos + volume).
  Future<void> saveSoundboardConfig({
    String? cableDeviceId,
    String? monitorDeviceId,
    bool? monitorEnabled,
    int? volume,
  }) async {
    await _dio.post('/api/Soundboard/config', data: {
      'cableDeviceId': cableDeviceId,
      'monitorDeviceId': monitorDeviceId,
      'monitorEnabled': monitorEnabled,
      'volume': volume,
    });
  }
}
