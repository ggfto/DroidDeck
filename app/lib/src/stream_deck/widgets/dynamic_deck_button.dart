import 'package:droiddeck/src/stream_deck/widgets/deck_grid_button.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/core/core.dart';
import 'package:flutter/material.dart';
import 'package:droiddeck/src/stream_deck/actions/action_catalog.dart';

import '../buttons/colors.dart';
import 'info_deck_button.dart';
import 'editor/info_section.dart';
import 'slider_deck_button.dart';
import 'usage_bars.dart';

class DynamicDeckButton extends StatelessWidget {
  final DeckButton button;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  /// Ver [DeckGridButton.liveKeys].
  final bool liveKeys;

  const DynamicDeckButton({
    super.key,
    required this.button,
    required this.onTap,
    required this.onLongPress,
    this.liveKeys = false,
  });

  /// O que o leitor de tela diz além do texto que a tecla já mostra (os dois se juntam
  /// num só anúncio). Tecla com rótulo próprio: nada a mais, senão o nome sai repetido.
  /// Medidores dizem o que medem; tecla só com ícone diz o tipo de ação.
  String? _semanticLabel() {
    final type = button.dynamicType;
    if (type != null && type.isNotEmpty && type != 'none') {
      if (InfoSection.labels[type] != null)
        return tr(InfoSection.labels[type]!);
      return switch (type) {
        'cpu_monitor' => tr('Uso da CPU'),
        'memory_monitor' => tr('Uso da memória'),
        'gpu_monitor' => tr('Uso da GPU'),
        'network_monitor' => tr('Rede'),
        'system_monitor' => tr('CPU e memória'),
        'disk_monitor' => tr('Espaço em disco'),
        _ => null,
      };
    }
    if (button.label?.trim().isNotEmpty ?? false) return null;
    if (button.action?.type == 'open_profile') return tr('Pasta');
    final schema = ActionCatalog.instance.find(button.action?.type);
    return schema?.title ?? tr('Botão');
  }

  @override
  Widget build(BuildContext context) {
    // Junta o rótulo daqui com o estado (ligado/desligado) que o visual da tecla informa.
    return MergeSemantics(
      child: Semantics(
        button: true,
        label: _semanticLabel(),
        child: _build(context),
      ),
    );
  }

  Widget _build(BuildContext context) {
    // If no dynamic type, render standard button
    if (button.dynamicType == null ||
        button.dynamicType!.isEmpty ||
        button.dynamicType == 'none') {
      return DeckGridButton(
        button: button,
        onTap: onTap,
        onLongPress: onLongPress,
        liveKeys: liveKeys,
      );
    }

    // Controle deslizante de volume.
    if (sliderTypes.contains(button.dynamicType)) {
      return SliderDeckButton(
        button: button,
        onTap: onTap,
        onLongPress: onLongPress,
        interactive: !WebMode.configurator,
      );
    }

    // Relógio, clima, tocando agora, comando e URL.
    if (infoTypes.contains(button.dynamicType)) {
      return InfoDeckButton(
        button: button,
        onTap: onTap,
        onLongPress: onLongPress,
        color: parseHexColor(button.backgroundColor),
      );
    }

    // Dynamic Rendering
    final signalR = Injector.get<SignalRService>();
    final stats = signalR.systemStats.watch(context);

    return DeckTile(
      color: parseHexColor(button.backgroundColor),
      onTap: onTap,
      onLongPress: onLongPress,
      child: _buildDynamicContent(context, stats, button.dynamicType!),
    );
  }

  Widget _buildDynamicContent(
    BuildContext context,
    SystemStats stats,
    String type,
  ) {
    switch (type) {
      case 'cpu_monitor':
        return _buildCpuMonitor(stats);
      case 'memory_monitor':
        return _buildMemoryMonitor(stats);
      case 'gpu_monitor':
        return _buildGpuMonitor(stats);
      case 'network_monitor':
        return _buildNetworkMonitor(stats);
      case 'system_monitor':
        return _buildSystemMonitor(stats);
      case 'disk_monitor':
        return _buildDiskMonitor(stats);
      default:
        return Center(child: Text(tr('Desconhecido: {type}', {'type': type})));
    }
  }

  Map<String, String> get _params => button.dynamicParameters ?? const {};

  /// Opção "Barra" dos medidores (o padrão é o anel).
  bool get _bars => _params['style'] == 'bars';

  static double _safe(double v) => v.isNaN || v.isInfinite ? 0 : v;

  String? get _label => button.label?.trim().isNotEmpty == true ? button.label!.trim() : null;

  /// CPU e memória juntas (e a GPU, se ligada), em barras como o uso de IA.
  Widget _buildSystemMonitor(SystemStats stats) {
    final ram = _safe(stats.ramUsedPercent);
    return UsageBars(
      icon: Icons.memory,
      title: _label ?? 'PC',
      rows: [
        UsageRow('CPU', _safe(stats.cpuUsage)),
        UsageRow(tr('RAM'), ram,
            detail: stats.ramTotal > 0
                ? '${stats.ramUsedGb.toStringAsFixed(1)}/${stats.ramTotalGb.toStringAsFixed(1)} GB'
                : null),
        if (_params['gpu'] == 'true') UsageRow('GPU', _safe(stats.gpuUsage)),
        if (_params['net'] == 'true') ...[
          // Download e upload numa linha só, nas cores do medidor de rede.
          UsageRow.custom(Text.rich(
            TextSpan(children: [
              TextSpan(
                  text: '↓ ${_fmtRate(stats.netDown)}  ',
                  style: const TextStyle(color: Colors.greenAccent)),
              TextSpan(
                  text: '↑ ${_fmtRate(stats.netUp)}',
                  style: const TextStyle(color: Colors.lightBlueAccent)),
            ]),
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
          )),
        ],
      ],
    );
  }

  /// Uma unidade (anel ou barra) ou todas (uma barra por unidade).
  Widget _buildDiskMonitor(SystemStats stats) {
    final drive = _params['drive'] ?? '';
    String free(DiskStat d) => tr('{n} livres', {'n': formatBytes(d.freeBytes)});
    if (drive.isEmpty) {
      return UsageBars(
        icon: Icons.storage,
        title: _label ?? tr('Discos'),
        rows: [
          for (final d in stats.disks)
            UsageRow(d.name, d.usedPercent, detail: free(d)),
        ],
      );
    }
    final d = stats.disks.where((d) => d.name.toUpperCase() == drive.toUpperCase()).firstOrNull;
    if (d == null) {
      // Unidade que saiu (HD desligado) ou ainda sem a primeira leitura.
      return Center(
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Icon(Icons.storage, size: 18),
            Text(drive, style: const TextStyle(fontWeight: FontWeight.bold)),
            Text(tr('Sem dados'), style: const TextStyle(fontSize: 10)),
          ]),
        ),
      );
    }
    final name = _label ?? (d.label.isNotEmpty ? '${d.name} ${d.label}' : d.name);
    if (_bars) {
      return UsageBar(icon: Icons.storage, name: name, percent: d.usedPercent, detail: free(d));
    }
    return _gauge(
      percent: d.usedPercent,
      icon: Icons.storage,
      value: '${d.usedPercent.toStringAsFixed(0)}%',
      label: '${d.name} ${formatBytes(d.freeBytes)}',
    );
  }

  Widget _buildCpuMonitor(SystemStats stats) {
    double pct = stats.cpuUsage;
    if (pct.isNaN || pct.isInfinite) pct = 0;
    if (_bars) return UsageBar(icon: Icons.memory, name: 'CPU', percent: pct);
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
    if (_bars) {
      return UsageBar(
        icon: Icons.memory_outlined,
        name: tr('RAM'),
        percent: pct,
        detail: stats.ramTotal > 0 ? detail : null,
      );
    }
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
    if (_bars) return UsageBar(icon: Icons.videogame_asset, name: 'GPU', percent: pct);
    return _gauge(
      percent: pct,
      icon: Icons.videogame_asset,
      value: '${pct.toStringAsFixed(0)}%',
      label: 'GPU',
    );
  }

  Widget _buildNetworkMonitor(SystemStats stats) {
    // FittedBox: com a unidade (KB/s) o texto não pode estourar num botão pequeno.
    return Center(
      child: FittedBox(
        fit: BoxFit.scaleDown,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.swap_vert, size: 18),
            Text(
              '↓ ${_fmtRate(stats.netDown)}',
              style: const TextStyle(
                color: Colors.greenAccent,
                fontWeight: FontWeight.bold,
                fontSize: 11,
              ),
            ),
            Text(
              '↑ ${_fmtRate(stats.netUp)}',
              style: const TextStyle(
                color: Colors.lightBlueAccent,
                fontWeight: FontWeight.bold,
                fontSize: 11,
              ),
            ),
            const Opacity(
              opacity: 0.7,
              child: Text('NET', style: TextStyle(fontSize: 10)),
            ),
          ],
        ),
      ),
    );
  }

  String _fmtRate(double kbps) {
    if (kbps >= 1024) return '${(kbps / 1024).toStringAsFixed(1)} MB/s';
    return '${kbps.toStringAsFixed(0)} KB/s';
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
        final fg = DeckTile.foregroundOf(context);
        final small = c.maxWidth < 70 || c.maxHeight < 70;
        return Stack(
          alignment: Alignment.center,
          children: [
            // Sempre um círculo: o anel segue o lado menor do botão, mesmo quando a
            // área não é quadrada (botão grande, celular deitado).
            Center(
              child: SizedBox.square(
                dimension:
                    ((c.maxWidth < c.maxHeight ? c.maxWidth : c.maxHeight) - 12)
                        .clamp(0.0, double.infinity),
                // O número já é lido no texto do centro; o anel não repete.
                child: ExcludeSemantics(
                  child: CircularProgressIndicator(
                    value: p,
                    strokeWidth: small ? 4 : 6,
                    backgroundColor: fg.withValues(alpha: 0.12),
                    valueColor: AlwaysStoppedAnimation<Color>(color),
                  ),
                ),
              ),
            ),
            // Encolhe o texto para caber dentro do anel em botões pequenos.
            Padding(
              padding: EdgeInsets.all(small ? 10 : 14),
              child: FittedBox(
                fit: BoxFit.scaleDown,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(icon, size: small ? 14 : 18),
                    Text(
                      value,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: small ? 12 : 15,
                      ),
                    ),
                    Text(
                      label,
                      style: TextStyle(
                        color: fg.withValues(alpha: 0.7),
                        fontSize: small ? 8 : 9,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.clip,
                    ),
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
