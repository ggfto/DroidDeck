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

  // Operação do Discord (crua, minúscula) se for um botão de Discord; senão null.
  String? get _discordOp {
    final a = widget.button.action;
    if (a == null || a.type != 'discord') return null;
    return (a.parameters['operation'] ?? 'toggleMute').toLowerCase();
  }

  // Comando de mídia (play/pause/next/...) se for um botão de mídia; senão null.
  String? get _mediaCommand {
    final a = widget.button.action;
    if (a == null || a.type != 'media') return null;
    return (a.parameters['command'] ?? 'playpause').toLowerCase();
  }

  IconData _mediaIconData(String cmd) {
    switch (cmd) {
      case 'next':
        return Icons.skip_next;
      case 'previous':
        return Icons.skip_previous;
      case 'stop':
        return Icons.stop;
      case 'pause':
        return Icons.pause;
      case 'play':
        return Icons.play_arrow;
      default: // playpause / toggle
        return Icons.play_arrow;
    }
  }

  String _mediaDefaultLabel(String cmd) {
    switch (cmd) {
      case 'next':
        return 'Próxima';
      case 'previous':
        return 'Anterior';
      case 'stop':
        return 'Parar';
      case 'pause':
        return 'Pause';
      case 'play':
        return 'Play';
      default:
        return 'Play/Pause';
    }
  }

  @override
  void initState() {
    super.initState();
    final p = _muteProcess;
    if (p != null) {
      // Consulta o estado de mute inicial uma vez (se ainda não conhecido).
      try {
        final sr = Injector.get<SignalRService>();
        if (!sr.muteStates.value.containsKey(p)) {
          Injector.get<DroidDeckClient>().getMuteState(p).then((m) {
            if (m != null && mounted) {
              final cur = Map<String, bool>.from(sr.muteStates.value);
              cur[p] = m;
              sr.muteStates.value = cur;
            }
          }).catchError((_) {});
        }
      } catch (_) {}
    }
    if (_discordOp != null) {
      try {
        final sr = Injector.get<SignalRService>();
        Injector.get<DroidDeckClient>().getDiscordState().then((s) {
          if (s != null && mounted) {
            sr.discordState.value = Map<String, dynamic>.from(s);
          }
        }).catchError((_) {});
      } catch (_) {}
    }
  }

  @override
  Widget build(BuildContext context) {
    final dop = _discordOp;
    if (dop != null) {
      // .value (não .watch(context)) é o idiomático dentro de Watch — garante rebuild.
      return Watch((context) {
        final ds = Injector.get<SignalRService>().discordState.value;
        return _renderDiscord(dop, ds);
      });
    }
    final p = _muteProcess;
    if (p == null) return _render(null);
    // Reage ao estado de mute vindo do backend (toque ou mudança externa).
    return Watch((context) {
      final muted = Injector.get<SignalRService>().muteStates.value[p];
      return _render(muted);
    });
  }

  /// Cor exibida quando o botão está "ativo" (toggle ligado). Configurável; padrão vermelho.
  Color _activeColor() =>
      _parseColor(widget.button.activeColor) ?? Colors.red[900]!;

  /// Atualiza o estado local na hora do toque (otimista): o botão pinta imediatamente,
  /// sem depender da volta do broadcast. O broadcast do backend confirma/corrige depois.
  void _handleTap() {
    try {
      final sr = Injector.get<SignalRService>();
      final dop = _discordOp;
      if (dop != null) {
        // Otimismo só para toggles (mute/deaf/modo). Os demais (entrar canal,
        // volume, etc.) atualizam pelo broadcast do backend.
        final cur = Map<String, dynamic>.from(sr.discordState.value);
        if (dop == 'togglemute') {
          cur['mute'] = !(cur['mute'] == true);
        } else if (dop.contains('deaf')) {
          cur['deaf'] = !(cur['deaf'] == true);
        } else if (dop.contains('voicemode') || dop.contains('ptt')) {
          cur['voiceMode'] = cur['voiceMode'] == 'PUSH_TO_TALK'
              ? 'VOICE_ACTIVITY'
              : 'PUSH_TO_TALK';
        } else {
          widget.onTap();
          return;
        }
        sr.discordState.value = cur;
      } else {
        final p = _muteProcess;
        if (p != null) {
          final op =
              (widget.button.action?.parameters['operation'] ?? 'toggleMute')
                  .toLowerCase();
          final cur = Map<String, bool>.from(sr.muteStates.value);
          final now = cur[p] == true;
          cur[p] = op == 'mute' ? true : (op == 'unmute' ? false : !now);
          sr.muteStates.value = cur;
        }
      }
    } catch (_) {}
    widget.onTap();
  }

  Widget _render(bool? muted) {
    final baseColor = _parseColor(widget.button.backgroundColor) ?? Colors.grey[850];
    final bg = muted == true ? _activeColor() : baseColor;
    // Label: o do botão; se vazio e for mídia, usa o rótulo padrão do comando.
    final label = (widget.button.label != null && widget.button.label!.isNotEmpty)
        ? widget.button.label!
        : (_mediaCommand != null ? _mediaDefaultLabel(_mediaCommand!) : null);

    return GestureDetector(
      onTap: _handleTap,
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
            if (label != null) ...[
              const SizedBox(height: 4),
              Text(
                label,
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

  Widget _renderDiscord(String op, Map<String, dynamic> ds) {
    final params = widget.button.action?.parameters ?? const <String, String>{};
    IconData icon;
    bool active = false;
    String defLabel;

    if (op.contains('deaf')) {
      active = ds['deaf'] == true;
      icon = active ? Icons.headset_off : Icons.headset_mic;
      defLabel = 'Deafen';
    } else if (op == 'joinchannel') {
      final ch = params['channelId'];
      active = ch != null && ch.isNotEmpty && ds['channelId'] == ch;
      icon = active ? Icons.headset : Icons.login;
      defLabel = (params['channelName']?.isNotEmpty ?? false)
          ? params['channelName']!
          : 'Canal';
    } else if (op == 'disconnect' || op == 'leavechannel') {
      icon = Icons.call_end;
      defLabel = 'Sair';
    } else if (op.contains('inputvolume')) {
      icon = op.contains('up') ? Icons.mic : Icons.mic_none;
      defLabel = op.contains('up') ? 'Mic +' : 'Mic −';
    } else if (op.contains('outputvolume')) {
      icon = op.contains('up') ? Icons.volume_up : Icons.volume_down;
      defLabel = op.contains('up') ? 'Som +' : 'Som −';
    } else if (op.contains('voicemode') || op.contains('ptt')) {
      active = ds['voiceMode'] == 'PUSH_TO_TALK';
      icon = active ? Icons.record_voice_over : Icons.graphic_eq;
      defLabel = active ? 'PTT' : 'Voz';
    } else if (op.startsWith('usermute')) {
      icon = Icons.person_off;
      defLabel =
          (params['userName']?.isNotEmpty ?? false) ? params['userName']! : 'Mutar';
    } else if (op == 'uservolume') {
      icon = Icons.person;
      defLabel =
          (params['userName']?.isNotEmpty ?? false) ? params['userName']! : 'Vol';
    } else {
      // toggleMute (padrão)
      active = ds['mute'] == true;
      icon = active ? Icons.mic_off : Icons.mic;
      defLabel = 'Mute';
    }

    final baseColor =
        _parseColor(widget.button.backgroundColor) ?? Colors.grey[850];
    final bg = active ? _activeColor() : baseColor;
    final label =
        (widget.button.label != null && widget.button.label!.isNotEmpty)
            ? widget.button.label!
            : defLabel;
    return GestureDetector(
      onTap: _handleTap,
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
            Icon(icon, size: 30, color: Colors.white),
            const SizedBox(height: 4),
            Text(label,
                textAlign: TextAlign.center,
                style: const TextStyle(
                    color: Colors.white,
                    fontSize: 10,
                    overflow: TextOverflow.ellipsis),
                maxLines: 2),
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
    // Botão de mídia sem ícone custom: usa o ícone do comando.
    final mc = _mediaCommand;
    if (mc != null) {
      return Icon(_mediaIconData(mc), size: 32, color: Colors.white);
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
