import 'package:companion/src/stream_deck/widgets/deck_grid_button.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/core/core.dart';
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
      case 'gpu_monitor':
        return _buildGpuMonitor(stats);
      case 'network_monitor':
        return _buildNetworkMonitor(stats);
      default:
        return Center(
            child:
                Text('Unknown: $type', style: TextStyle(color: Colors.white)));
    }
  }

  Widget _buildCpuMonitor(SystemStats stats) {
    double pct = stats.cpuUsage;
    if (pct.isNaN || pct.isInfinite) pct = 0;
    return _gauge(
      percent: pct,
      icon: Icons.memory,
      value: '${pct.toStringAsFixed(0)}%',
      label: 'CPU',
    );
  }

  Widget _buildMemoryMonitor(SystemStats stats) {
    double pct = stats.ramUsedPercent;
    if (pct.isNaN || pct.isInfinite) pct = 0;
    final detail = stats.ramTotal > 0
        ? '${stats.ramUsedGb.toStringAsFixed(1)}/${stats.ramTotalGb.toStringAsFixed(1)}G'
        : 'RAM';
    return _gauge(
      percent: pct,
      icon: Icons.memory_outlined,
      value: '${pct.toStringAsFixed(0)}%',
      label: detail,
    );
  }

  Widget _buildGpuMonitor(SystemStats stats) {
    double pct = stats.gpuUsage;
    if (pct.isNaN || pct.isInfinite) pct = 0;
    return _gauge(
      percent: pct,
      icon: Icons.videogame_asset,
      value: '${pct.toStringAsFixed(0)}%',
      label: 'GPU',
    );
  }

  Widget _buildNetworkMonitor(SystemStats stats) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.swap_vert, color: Colors.white, size: 18),
          Text('↓ ${_fmtRate(stats.netDown)}',
              style: const TextStyle(
                  color: Colors.greenAccent,
                  fontWeight: FontWeight.bold,
                  fontSize: 12)),
          Text('↑ ${_fmtRate(stats.netUp)}',
              style: const TextStyle(
                  color: Colors.lightBlueAccent,
                  fontWeight: FontWeight.bold,
                  fontSize: 12)),
          const Text('NET', style: TextStyle(color: Colors.white70, fontSize: 9)),
        ],
      ),
    );
  }

  String _fmtRate(double kbps) {
    if (kbps >= 1024) return '${(kbps / 1024).toStringAsFixed(1)}M';
    return '${kbps.toStringAsFixed(0)}K';
  }

  Color _usageColor(double pct) {
    if (pct > 85) return Colors.redAccent;
    if (pct > 60) return Colors.orangeAccent;
    return Colors.greenAccent;
  }

  /// Medidor circular: anel colorido pela faixa de uso + ícone/valor/label no centro.
  Widget _gauge({
    required double percent,
    required IconData icon,
    required String value,
    required String label,
  }) {
    final p = (percent / 100).clamp(0.0, 1.0);
    final color = _usageColor(percent);
    return LayoutBuilder(
      builder: (context, c) {
        final small = c.maxWidth < 70 || c.maxHeight < 70;
        return Stack(
          alignment: Alignment.center,
          children: [
            Positioned.fill(
              child: Padding(
                padding: const EdgeInsets.all(6),
                child: CircularProgressIndicator(
                  value: p,
                  strokeWidth: small ? 4 : 6,
                  backgroundColor: Colors.white12,
                  valueColor: AlwaysStoppedAnimation<Color>(color),
                ),
              ),
            ),
            Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, color: Colors.white, size: small ? 14 : 18),
                Text(value,
                    style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: small ? 12 : 15)),
                Text(label,
                    style: TextStyle(
                        color: Colors.white70, fontSize: small ? 8 : 9),
                    maxLines: 1,
                    overflow: TextOverflow.clip),
              ],
            ),
          ],
        );
      },
    );
  }
}
