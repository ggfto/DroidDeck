import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';

class HomeController {
  final DroidDeckClient _client = Injector.get<DroidDeckClient>();
  final SignalRService _signalR = Injector.get<SignalRService>();

  late final outputs = signal<List<MixerEntity>>([]);
  late final inputs = signal<List<MixerEntity>>([]);
  late final isLoading = signal(false);
  late final error = signal<String?>(null);
  late final isFullscreen = signal(false);

  Timer? _pollingTimer;
  Timer? _signalRRefreshDebounce;
  Timer? _mediaRefreshDebounce;
  void Function()? _volumeSubDispose;
  void Function()? _mediaSubDispose;

  HomeController() {
    // Subscribe to SignalR volume updates and refresh when they arrive.
    // Guarda o dispose da assinatura pra cancelar no dispose() — senao o callback
    // continua vivo e dispara fetch num controller ja descartado (leak + erro).
    // Estado de midia mudou em OUTRO lugar (botao do deck, outro celular): recarrega
    // o card de midia. Sem isto, o app so descobria a troca no poll de 60s.
    _mediaSubDispose = _signalR.mediaStateUpdates.subscribe((update) {
      if (update == null) return;
      _mediaRefreshDebounce?.cancel();
      _mediaRefreshDebounce =
          Timer(const Duration(milliseconds: 400), fetchMediaSessions);
    });

    _volumeSubDispose = _signalR.volumeUpdates.subscribe((updates) {
      if (updates.isEmpty) return;

      // Debounce to avoid excessive refreshes during rapid slider changes
      _signalRRefreshDebounce?.cancel();
      _signalRRefreshDebounce = Timer(const Duration(milliseconds: 500), () {
        fetchOutputs();
        fetchInputs();
      });
    });
  }

  void startPolling() {
    // Initial fetch
    fetchOutputs();
    fetchInputs();
    fetchMediaSessions();

    // Poll every 60 seconds (SignalR handles real-time updates)
    _pollingTimer = Timer.periodic(const Duration(seconds: 60), (_) {
      fetchOutputs();
      fetchInputs();
      fetchMediaSessions();
    });
  }

  void stopPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
  }

  Future<void> fetchOutputs() async {
    // Don't show loading on periodic refresh
    final isInitialLoad = outputs.value.isEmpty;
    if (isInitialLoad) {
      isLoading.value = true;
    }

    try {
      final res = await _client.getOutputs();
      outputs.value = res;
      error.value = null;
    } catch (e) {
      error.value = e.toString();
      debugPrint('DEBUG ERROR (outputs): $e');
    } finally {
      if (isInitialLoad) {
        isLoading.value = false;
      }
    }
  }

  Future<void> fetchInputs() async {
    // Don't show loading on periodic refresh
    final isInitialLoad = inputs.value.isEmpty;
    if (isInitialLoad) {
      isLoading.value = true;
    }

    try {
      debugPrint('DEBUG: Fetching inputs...');
      final res = await _client.getInputs();
      debugPrint('DEBUG: Got ${res.length} input devices');
      inputs.value = res;
      error.value = null;

      // Debug: Print first input device
      if (res.isNotEmpty) {
        debugPrint('DEBUG: First input device title: ${res.first.device.title}');
      } else {
        debugPrint('DEBUG: No input devices found');
      }
    } catch (e) {
      error.value = e.toString();
      debugPrint('DEBUG ERROR (inputs): $e');
    } finally {
      if (isInitialLoad) {
        isLoading.value = false;
      }
    }
  }

  // Send volume change and refresh immediately
  Future<void> setVolume(String id, int volume) async {
    try {
      await _client.setOutput(id, MixerData(volume: volume));
      // Refresh immediately - slider won't jump because of _isDragging flag
      fetchOutputs();
    } catch (e) {
      error.value = e.toString();
    }
  }

  Future<void> toggleMute(String id, bool currentMute) async {
    try {
      await _client.setOutput(id, MixerData(mute: !currentMute));
      // Refresh immediately on mute for instant feedback
      fetchOutputs();
    } catch (e) {
      error.value = e.toString();
    }
  }

  Future<void> setChannelVolume(
      String deviceId, int sessionId, int volume) async {
    try {
      await _client.setOutput(
          deviceId, MixerData(session: sessionId, volume: volume));
      // Refresh immediately - slider won't jump because of _isDragging flag
      fetchOutputs();
    } catch (e) {
      error.value = e.toString();
    }
  }

  Future<void> toggleChannelMute(
      String deviceId, int sessionId, bool currentMute) async {
    try {
      await _client.setOutput(
          deviceId, MixerData(session: sessionId, mute: !currentMute));
      fetchOutputs();
    } catch (e) {
      error.value = e.toString();
    }
  }

  void dispose() {
    stopPolling();
    _signalRRefreshDebounce?.cancel();
    _mediaRefreshDebounce?.cancel();
    _volumeSubDispose?.call();
    _volumeSubDispose = null;
    _mediaSubDispose?.call();
    _mediaSubDispose = null;
  }

  void toggleFullscreen() {
    isFullscreen.value = !isFullscreen.value;
  }

  // Media Control methods
  late final mediaSessions = signal<Map<String, MediaSession>>({});

  Future<void> fetchMediaSessions() async {
    try {
      final sessions = await _client.getMediaSessions();
      debugPrint('DEBUG: Fetched ${sessions.length} media sessions');
      // Convert list to map by ID for easy lookup
      final sessionMap = <String, MediaSession>{};
      for (final session in sessions) {
        if (session.id != null) {
          sessionMap[session.id!] = session;
          debugPrint(
              'DEBUG: Media session ID: ${session.id} - Title: ${session.title}');
        }
      }
      mediaSessions.value = sessionMap;
    } catch (e) {
      debugPrint('DEBUG ERROR (media sessions): $e');
    }
  }

  Future<void> sendMediaCommand(String sessionId, String command) async {
    try {
      // A resposta ja traz o estado logo apos o comando — antes havia um
      // Future.delayed(500ms) as cegas antes de recarregar tudo, que tanto podia
      // ser cedo demais (voltava o estado velho) quanto atraso puro na UI.
      final result = await _client.sendMediaCommand(sessionId, command);

      final playing = result['playing'];
      // O backend redireciona o comando quando o id salvo nao existe mais, entao
      // o estado tem que ser aplicado na sessao que REALMENTE recebeu.
      final applied = result['sessionId'] as String? ?? sessionId;

      if (playing is bool) {
        final known = mediaSessions.value[applied];
        if (known != null) {
          mediaSessions.value = {
            ...mediaSessions.value,
            applied: known.copyWith(
                playbackStatus: playing ? 'Playing' : 'Paused'),
          };
        }
      }

      // Recarrega para pegar faixa/capa novas (next/previous trocam a musica).
      fetchMediaSessions();
    } catch (e) {
      debugPrint('DEBUG ERROR (send media command): $e');
    }
  }
}
