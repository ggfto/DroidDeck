import 'dart:convert';
import 'dart:typed_data';
import 'dart:ui' show VoidCallback;
import 'package:flutter/material.dart';

class VolumeControl extends StatefulWidget {
  final String label;
  final int volume;
  final bool isMuted;
  final ValueChanged<int> onVolumeChanged;
  final VoidCallback onMuteToggle;
  final String? iconBase64;

  const VolumeControl({
    super.key,
    required this.label,
    required this.volume,
    required this.isMuted,
    required this.onVolumeChanged,
    required this.onMuteToggle,
    this.iconBase64,
  });

  @override
  State<VolumeControl> createState() => _VolumeControlState();
}

class _VolumeControlState extends State<VolumeControl> {
  Uint8List? _cachedImageBytes;
  late int _currentVolume;
  late bool _currentMute;
  bool _isDragging = false;
  bool _isMuteChanging = false;

  @override
  void initState() {
    super.initState();
    _currentVolume = widget.volume;
    _currentMute = widget.isMuted;
    _decodeImage();
  }

  @override
  void didUpdateWidget(VolumeControl oldWidget) {
    super.didUpdateWidget(oldWidget);

    // Only update volume from props if not dragging
    if (!_isDragging && oldWidget.volume != widget.volume) {
      _currentVolume = widget.volume;
    }

    // Only update mute from props if not changing
    if (!_isMuteChanging && oldWidget.isMuted != widget.isMuted) {
      _currentMute = widget.isMuted;
    }

    if (oldWidget.iconBase64 != widget.iconBase64) {
      _decodeImage();
    }
  }

  void _decodeImage() {
    if (widget.iconBase64 == null || widget.iconBase64!.isEmpty) {
      _cachedImageBytes = null;
      return;
    }

    try {
      String base64String = widget.iconBase64!;
      if (base64String.startsWith('data:image')) {
        base64String = base64String.split(',')[1];
      }
      _cachedImageBytes = base64Decode(base64String);
    } catch (e) {
      print('Error decoding icon: $e');
      _cachedImageBytes = null;
    }
  }

  void _handleMuteToggle() {
    setState(() {
      _isMuteChanging = true;
      _currentMute = !_currentMute;
    });

    widget.onMuteToggle();

    // Reset flag after a delay
    Future.delayed(const Duration(milliseconds: 300), () {
      if (mounted) {
        setState(() {
          _isMuteChanging = false;
        });
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8, horizontal: 16),
      child: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Row(
                    children: [
                      if (_cachedImageBytes != null)
                        Padding(
                          padding: const EdgeInsets.only(right: 12.0),
                          child: Image.memory(
                            _cachedImageBytes!,
                            width: 32,
                            height: 32,
                            errorBuilder: (context, error, stackTrace) {
                              return const Icon(Icons.apps, size: 32);
                            },
                          ),
                        ),
                      Expanded(
                        child: Text(
                          widget.label,
                          style: Theme.of(context).textTheme.titleMedium,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  icon: Icon(
                    _currentMute ? Icons.volume_off : Icons.volume_up,
                    color: _currentMute ? Colors.red : null,
                  ),
                  onPressed: _handleMuteToggle,
                ),
              ],
            ),
            Row(
              children: [
                Expanded(
                  child: Slider(
                    value: _currentVolume.toDouble(),
                    min: 0,
                    max: 100,
                    label: _currentVolume.toString(),
                    onChanged: (val) {
                      setState(() {
                        _isDragging = true;
                        _currentVolume = val.toInt();
                      });
                    },
                    onChangeEnd: (val) {
                      setState(() {
                        _isDragging = false;
                      });
                      widget.onVolumeChanged(val.toInt());
                    },
                  ),
                ),
                Text('$_currentVolume%'),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
