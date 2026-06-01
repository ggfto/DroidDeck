import 'dart:async';
import 'package:companion_core/companion_core.dart';
import 'package:companion/src/services/signalr_service.dart';

class HomeController {
  final AnyDeckClient _client = Injector.get<AnyDeckClient>();
  final SignalRService _signalR = Injector.get<SignalRService>();

  late final outputs = signal<List<MixerEntity>>([]);
  late final inputs = signal<List<MixerEntity>>([]);
  late final isLoading = signal(false);
  late final error = signal<String?>(null);
  late final isFullscreen = signal(false);

  Timer? _pollingTimer;
  Timer? _signalRRefreshDebounce;

  HomeController() {
    // Subscribe to SignalR volume updates and refresh when they arrive
    _signalR.volumeUpdates.subscribe((updates) {
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
      print('DEBUG ERROR (outputs): $e');
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
      print('DEBUG: Fetching inputs...');
      final res = await _client.getInputs();
      print('DEBUG: Got ${res.length} input devices');
      inputs.value = res;
      error.value = null;

      // Debug: Print first input device
      if (res.isNotEmpty) {
        print('DEBUG: First input device title: ${res.first.device.title}');
      } else {
        print('DEBUG: No input devices found');
      }
    } catch (e) {
      error.value = e.toString();
      print('DEBUG ERROR (inputs): $e');
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
  }

  void toggleFullscreen() {
    isFullscreen.value = !isFullscreen.value;
  }

  // Media Control methods
  late final mediaSessions = signal<Map<String, MediaSession>>({});

  Future<void> fetchMediaSessions() async {
    try {
      final sessions = await _client.getMediaSessions();
      print('DEBUG: Fetched ${sessions.length} media sessions');
      // Convert list to map by ID for easy lookup
      final sessionMap = <String, MediaSession>{};
      for (final session in sessions) {
        if (session.id != null) {
          sessionMap[session.id!] = session;
          print(
              'DEBUG: Media session ID: ${session.id} - Title: ${session.title}');
        }
      }
      mediaSessions.value = sessionMap;
    } catch (e) {
      print('DEBUG ERROR (media sessions): $e');
    }
  }

  Future<void> sendMediaCommand(String sessionId, String command) async {
    try {
      await _client.sendMediaCommand(sessionId, command);

      // Wait a bit for the system to process the command and update status
      await Future.delayed(const Duration(milliseconds: 500));

      // Refresh immediately for instant UI feedback
      fetchMediaSessions();
    } catch (e) {
      print('DEBUG ERROR (send media command): $e');
    }
  }
}
