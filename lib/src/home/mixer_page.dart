import 'dart:async';
import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'home_controller.dart';

class MixerPage extends StatelessWidget {
  final String title;
  final List<MixerEntity> devices;
  final Future<void> Function() onRefresh;
  final HomeController controller;
  final bool isInput;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  const MixerPage({
    super.key,
    required this.title,
    required this.devices,
    required this.onRefresh,
    required this.controller,
    required this.isInput,
    required this.isFullscreen,
    required this.onToggleFullscreen,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton(
        onPressed: onToggleFullscreen,
        mini: true,
        backgroundColor: Colors.black45,
        child: Icon(
          isFullscreen ? Icons.fullscreen_exit : Icons.fullscreen,
          color: Colors.white,
        ),
      ),
      appBar: isFullscreen
          ? null
          : AppBar(
              title: Text(title),
              actions: [
                IconButton(
                  icon: const Icon(Icons.settings),
                  tooltip: 'Configurações',
                  onPressed: () {
                    Navigator.of(context).pushNamed('/config');
                  },
                ),
              ],
            ),
      body: RefreshIndicator(
        onRefresh: onRefresh,
        child: devices.isEmpty
            ? Center(
                child: Text(isInput
                    ? 'Nenhum dispositivo de entrada encontrado.'
                    : 'Nenhum dispositivo de saída encontrado.'),
              )
            : ListView.builder(
                itemCount: devices.length,
                itemBuilder: (context, index) {
                  final item = devices[index];
                  final device = item.device;
                  final channels = device.channels ?? [];

                  // Find media session
                  MediaSession? deviceMediaSession;
                  for (final channel in channels) {
                    String? appName = channel.description;
                    if (appName != null && appName.contains(':')) {
                      appName = appName.split(':')[0].trim();
                    }

                    var session = controller.mediaSessions.value[appName ?? ''];

                    if (session == null && appName != null) {
                      final appLower = appName.toLowerCase();
                      session =
                          controller.mediaSessions.value.values.firstWhere(
                        (s) => s.id?.toLowerCase().contains(appLower) ?? false,
                        orElse: () => MediaSession(),
                      );
                      if (session.id == null) session = null;
                    }

                    if (session != null) {
                      deviceMediaSession = session;
                      break;
                    }
                  }

                  return Card(
                    margin:
                        const EdgeInsets.symmetric(vertical: 4, horizontal: 8),
                    child: ExpansionTile(
                      leading: device.icon != null && device.icon!.isNotEmpty
                          ? _buildDeviceIcon(device.icon!)
                          : const Icon(Icons.speaker),
                      title: Row(
                        children: [
                          Expanded(
                            child:
                                Text(device.title ?? item.id ?? 'Desconhecido'),
                          ),
                          IconButton(
                            icon: Icon(
                              device.mute ? Icons.volume_off : Icons.volume_up,
                              color: device.mute ? Colors.red : null,
                            ),
                            onPressed: () =>
                                controller.toggleMute(item.id!, device.mute),
                            padding: EdgeInsets.zero,
                            constraints: const BoxConstraints(),
                          ),
                        ],
                      ),
                      subtitle: _ResponsiveSlider(
                        initialVolume: device.volume,
                        isMuted: device.mute,
                        onVolumeChange: (val) =>
                            controller.setVolume(item.id!, val),
                        showLabel: false,
                      ),
                      children: [
                        if (deviceMediaSession != null &&
                            deviceMediaSession.id != null)
                          Builder(
                            builder: (context) {
                              final session = deviceMediaSession!;
                              return MediaControlWidget(
                                session: session,
                                onCommand: (command) =>
                                    controller.sendMediaCommand(
                                  session.id!,
                                  command,
                                ),
                              );
                            },
                          ),
                        if (channels.isEmpty)
                          const Padding(
                            padding: EdgeInsets.all(16.0),
                            child: Text('Nenhum app reproduzindo áudio'),
                          )
                        else
                          ...channels.map((channel) {
                            return Padding(
                              padding:
                                  const EdgeInsets.symmetric(horizontal: 8.0),
                              child: VolumeControl(
                                label:
                                    channel.description ?? 'App ${channel.id}',
                                volume: channel.volume,
                                isMuted: channel.mute,
                                iconBase64: channel.icon,
                                onVolumeChanged: (val) =>
                                    controller.setChannelVolume(
                                  item.id!,
                                  channel.id,
                                  val,
                                ),
                                onMuteToggle: () =>
                                    controller.toggleChannelMute(
                                  item.id!,
                                  channel.id,
                                  channel.mute,
                                ),
                              ),
                            );
                          }),
                      ],
                    ),
                  );
                },
              ),
      ),
    );
  }

  Widget _buildDeviceIcon(String iconBase64) {
    try {
      String base64String = iconBase64;
      if (base64String.startsWith('data:image')) {
        base64String = base64String.split(',')[1];
      }

      final bytes = base64Decode(base64String);
      return Image.memory(
        bytes,
        width: 32,
        height: 32,
        errorBuilder: (context, error, stackTrace) {
          return const Icon(Icons.speaker, size: 32);
        },
      );
    } catch (e) {
      return const Icon(Icons.speaker, size: 32);
    }
  }
}

class _ResponsiveSlider extends StatefulWidget {
  final int initialVolume;
  final bool isMuted;
  final ValueChanged<int> onVolumeChange;
  final bool showLabel;

  const _ResponsiveSlider({
    required this.initialVolume,
    required this.isMuted,
    required this.onVolumeChange,
    this.showLabel = true,
  });

  @override
  State<_ResponsiveSlider> createState() => _ResponsiveSliderState();
}

class _ResponsiveSliderState extends State<_ResponsiveSlider> {
  late int _currentVolume;
  bool _isDragging = false;
  Timer? _debounceTimer;

  @override
  void initState() {
    super.initState();
    _currentVolume = widget.initialVolume;
  }

  @override
  void didUpdateWidget(_ResponsiveSlider oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!_isDragging && oldWidget.initialVolume != widget.initialVolume) {
      _currentVolume = widget.initialVolume;
    }
  }

  @override
  void dispose() {
    _debounceTimer?.cancel();
    super.dispose();
  }

  void _onVolumeChanged(double value) {
    setState(() {
      _isDragging = true;
      _currentVolume = value.toInt();
    });

    // Debounce: cancel previous timer and start new one
    _debounceTimer?.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 300), () {
      // Send update to server after 300ms of no changes
      widget.onVolumeChange(_currentVolume);
    });
  }

  void _onVolumeChangeEnd(double value) {
    setState(() {
      _isDragging = false;
    });

    // Cancel debounce timer and send final value immediately
    _debounceTimer?.cancel();
    widget.onVolumeChange(value.toInt());
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Expanded(
          child: Slider(
            value: _currentVolume.toDouble(),
            min: 0,
            max: 100,
            label: _currentVolume.toString(),
            onChanged: _onVolumeChanged,
            onChangeEnd: _onVolumeChangeEnd,
          ),
        ),
        if (widget.showLabel)
          Padding(
            padding: const EdgeInsets.only(left: 8.0),
            child: Text('$_currentVolume%'),
          ),
      ],
    );
  }
}
