import 'package:companion/core/core.dart';
import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';
import 'package:logging/logging.dart';

class SystemStats {
  final double cpuUsage; // % (0-100)
  final double ramUsage; // % usada (0-100)
  final double ramTotal; // MB
  final double ramAvailable; // MB
  final double netUp; // KB/s
  final double netDown; // KB/s
  final double gpuUsage; // % (0-100)

  SystemStats({
    this.cpuUsage = 0,
    this.ramUsage = 0,
    this.ramTotal = 0,
    this.ramAvailable = 0,
    this.netUp = 0,
    this.netDown = 0,
    this.gpuUsage = 0,
  });

  /// % de RAM em uso (usa o valor do backend; se faltar, calcula de total/disponível).
  double get ramUsedPercent => ramUsage > 0
      ? ramUsage
      : (ramTotal > 0 ? (ramTotal - ramAvailable) / ramTotal * 100 : 0);
  double get ramUsedGb => (ramTotal - ramAvailable) / 1024.0;
  double get ramTotalGb => ramTotal / 1024.0;

  factory SystemStats.fromJson(Map<String, dynamic> json) {
    double parseDouble(dynamic val) {
      if (val == null) return 0;
      if (val is num) return val.toDouble();
      if (val is String) return double.tryParse(val) ?? 0;
      return 0;
    }

    return SystemStats(
      cpuUsage: parseDouble(json['cpuUsage'] ?? json['CpuUsage']),
      ramUsage: parseDouble(json['ramUsage'] ?? json['RamUsage']),
      ramTotal: parseDouble(json['ramTotal'] ?? json['RamTotal']),
      ramAvailable: parseDouble(json['ramAvailable'] ?? json['RamAvailable']),
      netUp: parseDouble(json['netUpKBps'] ?? json['NetUpKBps']),
      netDown: parseDouble(json['netDownKBps'] ?? json['NetDownKBps']),
      gpuUsage: parseDouble(json['gpuUsage'] ?? json['GpuUsage']),
    );
  }
}

class SignalRService {
  HubConnection? _hubConnection;
  final _logger = Logger('SignalRService');
  // Signals
  final systemStats = signal<SystemStats>(SystemStats());
  final connectionStatus = signal<String>("Disconnected");
  final lastDebugData = signal<String>("No Data");
  final currentUrl =
      signal<String>("Not Initialized"); // DEBUG: Track current URL

  // NEW: Real-time Volume/Media signals
  final volumeUpdates = signal<Map<String, dynamic>>({});
  final mediaStateUpdates = signal<Map<String, dynamic>?>(null);

  // Incrementa quando o backend avisa que um perfil do deck mudou (sync ao vivo).
  final deckUpdated = signal<int>(0);

  SignalRService();

  Future<void> init(String baseUrl, {String? apiKey}) async {
    if (_hubConnection != null) return;

    // Normalize Base URL (handle relative path)
    String hubUrl = baseUrl;
    if (hubUrl.isEmpty) {
      if (kIsWeb) {
        // Use current window origin
        hubUrl = '${Uri.base.origin}/deckHub';
      } else {
        _logger.warning('SignalR init skipped: Base URL is empty on mobile');
        connectionStatus.value = "URL Empty";
        currentUrl.value = "Empty (init called with empty baseUrl)";
        return;
      }
    } else {
      hubUrl = '$baseUrl/deckHub';
    }

    // SignalR via WebSocket autentica pela query string (?access_token=).
    if (apiKey != null && apiKey.isNotEmpty) {
      final sep = hubUrl.contains('?') ? '&' : '?';
      hubUrl += '${sep}access_token=${Uri.encodeQueryComponent(apiKey)}';
    }

    currentUrl.value = hubUrl; // Track the URL being used
    _logger.info('Connecting to SignalR Hub at $hubUrl');
    connectionStatus.value = "Connecting...";

    try {
      _hubConnection = HubConnectionBuilder()
          .withUrl(hubUrl)
          .withAutomaticReconnect()
          .build();
    } catch (e) {
      _logger.severe('Error building HubConnection: $e');
      connectionStatus.value = "Build Error";
      return;
    }

    _hubConnection?.on('ReceiveSystemStats', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          // Debug Raw Data
          final raw = arguments[0].toString();
          lastDebugData.value = "Data: $raw";

          // SignalR parses JSON for us roughly, but we check type
          final data = arguments[0] as Map<String, dynamic>;
          systemStats.value = SystemStats.fromJson(data);
        } catch (e) {
          _logger.warning('Error parsing stats: $e');
          lastDebugData.value = "Parse Error: $e";
        }
      } else {
        lastDebugData.value = "Empty Args";
      }
    });

    // NEW: Listen for Volume updates
    _hubConnection?.on('ReceiveVolumeChange', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          final update = arguments[0] as Map<String, dynamic>;
          final deviceId = update['deviceId'] as String;
          final type = update['type'] as String;
          final data = update['data'] as Map<String, dynamic>;

          final current = Map<String, dynamic>.from(volumeUpdates.value);
          current[deviceId] = {
            'type': type,
            'data': data,
            'timestamp': DateTime.now().millisecondsSinceEpoch,
          };
          volumeUpdates.value = current;

          _logger.info('Volume update: $deviceId ($type)');
        } catch (e) {
          _logger.warning('Error parsing ReceiveVolumeChange: $e');
        }
      }
    });

    // NEW: Listen for Media State updates
    _hubConnection?.on('ReceiveMediaState', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          final update = arguments[0] as Map<String, dynamic>;
          mediaStateUpdates.value = update;
          _logger.info('Media update: ${update['sessionId']}');
        } catch (e) {
          _logger.warning('Error parsing ReceiveMediaState: $e');
        }
      }
    });

    // Deck mudou em outro cliente (ex.: editor no PC) → sinaliza para recarregar.
    _hubConnection?.on('ReceiveDeckUpdate', (arguments) {
      deckUpdated.value = deckUpdated.value + 1;
      _logger.info('Deck update recebido');
    });

    _hubConnection?.onclose(({error}) {
      connectionStatus.value = "Disconnected: $error";
    });

    _hubConnection?.onreconnecting(({error}) {
      connectionStatus.value = "Reconnecting...";
    });

    _hubConnection?.onreconnected(({connectionId}) {
      connectionStatus.value = "Connected";
    });

    try {
      await _hubConnection?.start();
      _logger.info('SignalR Connected');
      connectionStatus.value = "Connected";
    } catch (e) {
      _logger.severe('SignalR Connection Failed: $e');
      connectionStatus.value = "Conn Error: $e";
    }
  }

  Future<void> updateUrl(String baseUrl, {String? apiKey}) async {
    if (_hubConnection != null) {
      await _hubConnection!.stop();
      _hubConnection = null;
    }
    await init(baseUrl, apiKey: apiKey);
  }
}
