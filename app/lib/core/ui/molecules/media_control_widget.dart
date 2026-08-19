import 'dart:async';
import 'dart:convert';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';

class MediaControlWidget extends StatefulWidget {
  final MediaSession session;
  final ValueChanged<String> onCommand;

  const MediaControlWidget({
    super.key,
    required this.session,
    required this.onCommand,
  });

  @override
  State<MediaControlWidget> createState() => _MediaControlWidgetState();
}

class _MediaControlWidgetState extends State<MediaControlWidget> {
  /// Palpite mostrado entre o toque e a chegada do estado real. O backend responde
  /// ao comando ja com o estado aplicado, entao isto cobre so o tempo de rede — nao
  /// e mais a fonte da verdade por varios segundos, como quando o app dependia do
  /// poll seguinte pra descobrir o que tinha acontecido.
  String? _optimisticPlaybackStatus;

  /// Estado real vigente quando o palpite foi feito. Enquanto o servidor nao disser
  /// algo DIFERENTE disto, o palpite continua valendo.
  String? _statusWhenGuessed;

  Timer? _optimisticTimer;

  @override
  void didUpdateWidget(MediaControlWidget oldWidget) {
    super.didUpdateWidget(oldWidget);

    // Chegou estado novo do servidor: o palpite cumpriu o papel.
    //
    // A comparacao e contra o estado de QUANDO o palpite foi feito, nao contra o
    // widget anterior. Comparar oldWidget com o novo (como antes) so limpava o
    // palpite quando o status mudava entre dois updates consecutivos — se o comando
    // falhasse e o status ficasse parado no valor antigo, os dois eram iguais, a
    // limpeza nunca acontecia, e o botao mentia ate o timer de seguranca estourar.
    if (_optimisticPlaybackStatus != null &&
        widget.session.playbackStatus != _statusWhenGuessed) {
      _clearOptimistic();
    }
  }

  @override
  void dispose() {
    _optimisticTimer?.cancel();
    super.dispose();
  }

  void _clearOptimistic() {
    _optimisticTimer?.cancel();
    _optimisticTimer = null;
    _optimisticPlaybackStatus = null;
    _statusWhenGuessed = null;
  }

  void _handlePlayPause() {
    final currentStatus =
        _optimisticPlaybackStatus ?? widget.session.playbackStatus ?? '';
    final isPlaying = currentStatus.toLowerCase() == 'playing';

    setState(() {
      _optimisticTimer?.cancel();
      _statusWhenGuessed = widget.session.playbackStatus;
      _optimisticPlaybackStatus = isPlaying ? 'Paused' : 'Playing';

      // Rede de seguranca: se nenhuma atualizacao chegar (backend fora do ar, comando
      // recusado), o botao volta a mostrar o estado real em vez de ficar mentindo.
      _optimisticTimer = Timer(const Duration(seconds: 4), () {
        if (mounted) setState(_clearOptimistic);
      });
    });

    widget.onCommand('playpause');
  }

  @override
  Widget build(BuildContext context) {
    final displayStatus =
        _optimisticPlaybackStatus ?? widget.session.playbackStatus ?? '';
    final isPlaying = displayStatus.toLowerCase() == 'playing';

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Padding(
        padding: const EdgeInsets.all(12.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Track info with album art
            Row(
              children: [
                // Album art
                if (widget.session.thumbnailBase64 != null &&
                    widget.session.thumbnailBase64!.isNotEmpty)
                  _buildAlbumArt()
                else
                  Container(
                    width: 56,
                    height: 56,
                    decoration: BoxDecoration(
                      color: Colors.grey[300],
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Icon(Icons.music_note,
                        size: 32, color: Colors.grey),
                  ),
                const SizedBox(width: 12),
                // Track and artist
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        widget.session.title ?? 'Unknown Track',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        widget.session.artist ?? 'Unknown Artist',
                        style: Theme.of(context).textTheme.bodySmall,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            // Media control buttons
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                IconButton(
                  icon: const Icon(Icons.skip_previous),
                  onPressed: widget.session.canGoPrevious
                      ? () => widget.onCommand('previous')
                      : null,
                  iconSize: 48,
                ),
                const SizedBox(width: 16),
                IconButton(
                  icon: Icon(
                    isPlaying ? Icons.pause : Icons.play_arrow,
                  ),
                  onPressed: _handlePlayPause,
                  iconSize: 48,
                ),
                const SizedBox(width: 16),
                IconButton(
                  icon: const Icon(Icons.skip_next),
                  onPressed: widget.session.canGoNext
                      ? () => widget.onCommand('next')
                      : null,
                  iconSize: 48,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAlbumArt() {
    try {
      String base64String = widget.session.thumbnailBase64!;
      if (base64String.startsWith('data:image')) {
        base64String = base64String.split(',')[1];
      }

      final bytes = base64Decode(base64String);
      return ClipRRect(
        borderRadius: BorderRadius.circular(8),
        child: Image.memory(
          bytes,
          width: 56,
          height: 56,
          fit: BoxFit.cover,
          errorBuilder: (context, error, stackTrace) {
            return Container(
              width: 56,
              height: 56,
              decoration: BoxDecoration(
                color: Colors.grey[300],
                borderRadius: BorderRadius.circular(8),
              ),
              child: const Icon(Icons.music_note, size: 32, color: Colors.grey),
            );
          },
        ),
      );
    } catch (e) {
      return Container(
        width: 56,
        height: 56,
        decoration: BoxDecoration(
          color: Colors.grey[300],
          borderRadius: BorderRadius.circular(8),
        ),
        child: const Icon(Icons.music_note, size: 32, color: Colors.grey),
      );
    }
  }
}
