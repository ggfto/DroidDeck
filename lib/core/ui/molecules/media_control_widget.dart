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
  String? _optimisticPlaybackStatus;
  DateTime? _lastCommandTime;

  @override
  void didUpdateWidget(MediaControlWidget oldWidget) {
    super.didUpdateWidget(oldWidget);

    // If we recently sent a command (within 2s), ignore updates that match the OLD state
    // This prevents "flicker" where the polling returns the old state before the command takes effect
    if (_lastCommandTime != null &&
        DateTime.now().difference(_lastCommandTime!).inMilliseconds < 2000) {
      // Keep optimistic state
    } else if (oldWidget.session.playbackStatus !=
        widget.session.playbackStatus) {
      // State changed from outside and enough time passed, reset optimistic
      _optimisticPlaybackStatus = null;
    }
  }

  void _handlePlayPause() {
    final currentStatus =
        _optimisticPlaybackStatus ?? widget.session.playbackStatus ?? '';
    final isPlaying = currentStatus.toLowerCase() == 'playing';

    setState(() {
      _lastCommandTime = DateTime.now();
      _optimisticPlaybackStatus = isPlaying ? 'Paused' : 'Playing';
    });

    widget.onCommand('playpause');

    // Clear optimistic state after timeout just in case
    Future.delayed(const Duration(seconds: 4), () {
      if (mounted &&
          _lastCommandTime != null &&
          DateTime.now().difference(_lastCommandTime!).inSeconds >= 4) {
        setState(() {
          _optimisticPlaybackStatus = null;
          _lastCommandTime = null;
        });
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final displayStatus =
        _optimisticPlaybackStatus ?? widget.session.playbackStatus ?? '';
    final isPlaying = displayStatus.toLowerCase() == 'playing';

    // Debug: print playback status
    print(
        'DEBUG MediaControl: playbackStatus="${widget.session.playbackStatus}", optimistic="$_optimisticPlaybackStatus", isPlaying=$isPlaying');

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
