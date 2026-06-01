import 'package:companion/src/stream_deck/widgets/deck_grid_button.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion_core/companion_core.dart';
import 'package:flutter/material.dart';

class DynamicDeckButton extends StatelessWidget {
  final DeckButton button;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  const DynamicDeckButton({
    super.key,
    required this.button,
    required this.onTap,
    required this.onLongPress,
  });

  Color _safeParseColor(String? hexString) {
    if (hexString == null || hexString.isEmpty) return Colors.grey[800]!;
    try {
      final buffer = StringBuffer();
      if (hexString.length == 6 || hexString.length == 7) buffer.write('ff');
      buffer.write(hexString.replaceFirst('#', ''));
      return Color(int.parse(buffer.toString(), radix: 16));
    } catch (_) {
      return Colors.grey[800]!;
    }
  }

  @override
  Widget build(BuildContext context) {
    // If no dynamic type, render standard button
    if (button.dynamicType == null ||
        button.dynamicType!.isEmpty ||
        button.dynamicType == 'none') {
      return DeckGridButton(
        button: button,
        onTap: onTap,
        onLongPress: onLongPress,
      );
    }

    // Dynamic Rendering
    final signalR = Injector.get<SignalRService>();
    final stats = signalR.systemStats.watch(context);

    // Common Container Styles
    final bgColor = _safeParseColor(button.backgroundColor);

    return InkWell(
      onTap: onTap,
      onLongPress: onLongPress,
      child: Container(
        decoration: BoxDecoration(
          color: bgColor,
          borderRadius: BorderRadius.circular(10),
          boxShadow: [
            BoxShadow(
                color: Colors.black45, offset: Offset(2, 2), blurRadius: 4),
          ],
        ),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: _buildDynamicContent(context, stats, button.dynamicType!),
        ),
      ),
    );
  }

  Widget _buildDynamicContent(
      BuildContext context, SystemStats stats, String type) {
    switch (type) {
      case 'cpu_monitor':
        return _buildCpuMonitor(stats);
      case 'memory_monitor':
        return _buildMemoryMonitor(stats);
      default:
        return Center(
            child:
                Text('Unknown: $type', style: TextStyle(color: Colors.white)));
    }
  }

  Widget _buildCpuMonitor(SystemStats stats) {
    // Color gradient based on usage
    // Handle potential NaN or Infinity
    double safeUsage = stats.cpuUsage;
    if (safeUsage.isNaN || safeUsage.isInfinite) safeUsage = 0;

    final usage = (safeUsage / 100.0).clamp(0.0, 1.0);
    Color barColor = Colors.green;
    if (usage > 0.6) barColor = Colors.orange;
    if (usage > 0.85) barColor = Colors.red;

    return Stack(
      alignment: Alignment.bottomCenter,
      children: [
        // Background Bar
        FractionallySizedBox(
          heightFactor: usage,
          widthFactor: 1.0,
          alignment: Alignment.bottomCenter,
          child: Container(color: barColor.withOpacity(0.5)),
        ),
        Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.memory, color: Colors.white, size: 24),
              const SizedBox(height: 4),
              Text('${stats.cpuUsage.toStringAsFixed(0)}%',
                  style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                      shadows: [Shadow(color: Colors.black, blurRadius: 2)])),
              const Text('CPU',
                  style: TextStyle(color: Colors.white70, fontSize: 10)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildMemoryMonitor(SystemStats stats) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.storage, color: Colors.white, size: 24),
          const SizedBox(height: 4),
          Text('${(stats.ramAvailable / 1024).toStringAsFixed(1)} GB',
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 14,
              )),
          const Text('FREE RAM',
              style: TextStyle(color: Colors.white70, fontSize: 10)),
        ],
      ),
    );
  }
}
