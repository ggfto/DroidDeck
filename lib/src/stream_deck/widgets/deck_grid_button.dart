import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:flutter/material.dart';

class DeckGridButton extends StatefulWidget {
  final DeckButton button;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  const DeckGridButton({
    super.key,
    required this.button,
    required this.onTap,
    required this.onLongPress,
  });

  @override
  State<DeckGridButton> createState() => _DeckGridButtonState();
}

class _DeckGridButtonState extends State<DeckGridButton> {
  /// Se o botão é um toggle de mute (ação mixer mute/toggle com processName),
  /// retorna o nome do processo-alvo; senão null (botão comum).
  String? get _muteProcess {
    final a = widget.button.action;
    if (a == null || a.type != 'mixer') return null;
    final op = (a.parameters['operation'] ?? 'toggleMute').toLowerCase();
    final pn = a.parameters['processName'];
    if (pn != null && pn.isNotEmpty && op.contains('mute')) return pn;
    return null;
  }

  @override
  void initState() {
    super.initState();
    final p = _muteProcess;
    if (p == null) return;
    // Consulta o estado de mute inicial uma vez (se ainda não conhecido).
    try {
      final sr = Injector.get<SignalRService>();
      if (!sr.muteStates.value.containsKey(p)) {
        Injector.get<AnyDeckClient>().getMuteState(p).then((m) {
          if (m != null && mounted) {
            final cur = Map<String, bool>.from(sr.muteStates.value);
            cur[p] = m;
            sr.muteStates.value = cur;
          }
        }).catchError((_) {});
      }
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final p = _muteProcess;
    if (p == null) return _render(null);
    // Reage ao estado de mute vindo do backend (toque ou mudança externa).
    return Watch((context) {
      final muted = Injector.get<SignalRService>().muteStates.watch(context)[p];
      return _render(muted);
    });
  }

  Widget _render(bool? muted) {
    final baseColor = _parseColor(widget.button.backgroundColor) ?? Colors.grey[850];
    final bg = muted == true ? Colors.red[900] : baseColor;

    return GestureDetector(
      onTap: widget.onTap,
      onLongPress: widget.onLongPress,
      child: Container(
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(8),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.3),
              blurRadius: 4,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            _buildIcon(muted),
            if (widget.button.label != null && widget.button.label!.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                widget.button.label!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 10,
                  overflow: TextOverflow.ellipsis,
                ),
                maxLines: 2,
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildIcon(bool? muted) {
    // Toggle de mute: o ícone reflete o estado real.
    if (muted != null) {
      return Icon(muted ? Icons.volume_off : Icons.volume_up,
          size: 32, color: Colors.white);
    }
    if (widget.button.iconBase64 != null &&
        widget.button.iconBase64!.isNotEmpty) {
      return _buildBase64Icon();
    }
    if (widget.button.iconName != null && widget.button.iconName!.isNotEmpty) {
      return Icon(_getIconData(widget.button.iconName!),
          size: 32, color: Colors.white);
    }
    return const Icon(Icons.touch_app, size: 32, color: Colors.white54);
  }

  Widget _buildBase64Icon() {
    try {
      String base64String = widget.button.iconBase64!;
      if (base64String.startsWith('data:image')) {
        base64String = base64String.split(',')[1];
      }
      final bytes = base64Decode(base64String);
      return Image.memory(
        bytes,
        width: 32,
        height: 32,
        errorBuilder: (_, __, ___) =>
            const Icon(Icons.error, color: Colors.red),
      );
    } catch (_) {
      return const Icon(Icons.broken_image, color: Colors.white54);
    }
  }

  Color? _parseColor(String? hexString) {
    if (hexString == null || hexString.isEmpty) return null;
    try {
      final buffer = StringBuffer();
      if (hexString.length == 6 || hexString.length == 7) buffer.write('ff');
      buffer.write(hexString.replaceFirst('#', ''));
      return Color(int.parse(buffer.toString(), radix: 16));
    } catch (_) {
      return null;
    }
  }

  IconData _getIconData(String name) {
    switch (name.toLowerCase()) {
      case 'play':
        return Icons.play_arrow;
      case 'pause':
        return Icons.pause;
      case 'volume_up':
        return Icons.volume_up;
      case 'volume_off':
        return Icons.volume_off;
      case 'mic':
        return Icons.mic;
      case 'mic_off':
        return Icons.mic_off;
      case 'keyboard':
        return Icons.keyboard;
      case 'launch':
        return Icons.rocket_launch;
      case 'home':
        return Icons.home;
      case 'settings':
        return Icons.settings;
      case 'code':
        return Icons.code;
      case 'videocam':
        return Icons.videocam;
      case 'call_end':
        return Icons.call_end;
      case 'folder':
        return Icons.folder;
      case 'save':
        return Icons.save;
      case 'delete':
        return Icons.delete;
      default:
        return Icons.extension;
    }
  }
}
