import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/src/stream_deck/layout/desktop_layout.dart';
import 'package:companion/src/stream_deck/stream_deck_controller.dart';
import 'package:companion/src/stream_deck/widgets/dynamic_deck_button.dart';
import 'package:companion/src/stream_deck/widgets/button_editor_dialog.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:collection/collection.dart';

class StreamDeckPage extends StatefulWidget {
  final VoidCallback onToggleFullscreen;
  final bool isFullscreen;

  const StreamDeckPage({
    super.key,
    required this.onToggleFullscreen,
    required this.isFullscreen,
  });

  @override
  State<StreamDeckPage> createState() => _StreamDeckPageState();
}

class _StreamDeckPageState extends State<StreamDeckPage> {
  final StreamDeckController controller = StreamDeckController();
  late PageController _pageController;
  void Function()? _deckSyncCleanup;
  final List<String> _navStack = []; // pilha de pastas (perfis) abertas
  DeckProfile? _rootProfile; // perfil-raiz antes de entrar em pastas
  int? _reportedCols; // última grade reportada ao PC (evita reenvio)
  int? _reportedRows;

  @override
  void initState() {
    super.initState();
    controller.loadProfiles();

    // Sync ao vivo: recarrega os perfis quando o backend avisa que o deck mudou
    // (ex.: alguém editou no configurador web do PC).
    final signalR = Injector.get<SignalRService>();
    var lastSeen = signalR.deckUpdated.value;
    _deckSyncCleanup = effect(() {
      final v = signalR.deckUpdated.value;
      if (v != lastSeen) {
        lastSeen = v;
        controller.loadProfiles();
      }
    });

    _pageController = PageController();
  }

  @override
  void dispose() {
    _deckSyncCleanup?.call();
    _pageController.dispose();
    super.dispose();
  }

  DeckProfile? _folderProfile(List<DeckProfile> profiles) {
    if (_navStack.isEmpty) return null;
    try {
      return profiles.firstWhere((p) => p.id == _navStack.last);
    } catch (_) {
      return null;
    }
  }

  /// Trata o toque: abre pasta (open_profile), volta (back) ou executa a ação.
  void _handlePress(DeckButton b, List<DeckProfile> profiles) {
    // Botão-monitor (CPU/GPU/RAM/Rede) é só display: não executa ação ao tocar.
    if (b.dynamicType != null && b.dynamicType!.isNotEmpty) return;
    final type = b.action?.type;
    if (type == 'open_profile') {
      final targetId = b.action!.parameters['profileId'];
      DeckProfile? target;
      try {
        target = profiles.firstWhere((p) => p.id == targetId);
      } catch (_) {
        target = null;
      }
      if (target != null) {
        setState(() {
          _rootProfile ??= controller.currentProfile.value;
          _navStack.add(target!.id);
          controller.currentProfile.value = target;
        });
      }
      return;
    }
    if (type == 'back') {
      _goBack(profiles);
      return;
    }
    controller.executeButtonAction(b);
  }

  void _goBack(List<DeckProfile> profiles) {
    setState(() {
      if (_navStack.isNotEmpty) _navStack.removeLast();
      if (_navStack.isEmpty) {
        controller.currentProfile.value =
            _rootProfile ?? controller.currentProfile.value;
        _rootProfile = null;
      } else {
        try {
          controller.currentProfile.value =
              profiles.firstWhere((p) => p.id == _navStack.last);
        } catch (_) {}
      }
    });
  }

  void _editButton(DeckButton button) {
    showDialog(
      context: context,
      builder: (context) => ButtonEditorDialog(
        button: button,
        onSave: controller.updateButton,
      ),
    );
  }

  /// Calcula quantos botões (cols×rows) cabem INTEIROS no espaço disponível
  /// (já dentro da SafeArea) e o tamanho de cada botão pra preencher sem rolagem.
  _GridFit _computeGrid(double w, double h) {
    const spacing = 8.0;
    const target = 96.0; // tamanho-alvo do botão (confortável pra tocar)
    int cols = ((w + spacing) / (target + spacing)).floor();
    int rows = ((h + spacing) / (target + spacing)).floor();
    if (cols < 1) cols = 1;
    if (cols > 20) cols = 20;
    if (rows < 1) rows = 1;
    if (rows > 20) rows = 20;
    final double cellW = (w - spacing * (cols - 1)) / cols;
    final double cellH = (h - spacing * (rows - 1)) / rows;
    double cell = cellW < cellH ? cellW : cellH;
    if (cell < 1) cell = 1;
    return _GridFit(cols: cols, rows: rows, cell: cell, spacing: spacing);
  }

  /// O celular é a fonte da verdade da grade: reporta ao PC quando ela muda
  /// (ex.: rotação). Só envia se mudou, fora do ciclo de build.
  void _maybeReportLayout(_GridFit g) {
    if (kIsWeb) return;
    if (_reportedCols == g.cols && _reportedRows == g.rows) return;
    _reportedCols = g.cols;
    _reportedRows = g.rows;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      try {
        Injector.get<DroidDeckClient>().saveLayout(g.rows, g.cols);
      } catch (_) {}
    });
  }

  /// Grade fixa cols×rows, centralizada, sem rolagem.
  Widget _buildButtonGrid(
      DeckProfile profile, List<DeckProfile> profiles, _GridFit g) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: List.generate(g.rows, (row) {
          return Padding(
            padding: EdgeInsets.only(bottom: row < g.rows - 1 ? g.spacing : 0),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: List.generate(g.cols, (col) {
                final existing = profile.buttons.firstWhereOrNull(
                  (b) => b.row == row && b.column == col,
                );
                return Padding(
                  padding:
                      EdgeInsets.only(right: col < g.cols - 1 ? g.spacing : 0),
                  child: SizedBox(
                    width: g.cell,
                    height: g.cell,
                    child: _buildCell(row, col, existing, profiles),
                  ),
                );
              }),
            ),
          );
        }),
      ),
    );
  }

  Widget _buildCell(
      int row, int col, DeckButton? existing, List<DeckProfile> profiles) {
    if (existing == null) {
      final novo = DeckButton(
        id: '',
        label: 'New Button',
        row: row,
        column: col,
        action: DeckAction(type: 'none'),
      );
      return InkWell(
        onTap: () => _editButton(novo),
        onLongPress: () => _editButton(novo),
        child: Container(
          decoration: BoxDecoration(
            color: Colors.grey[900],
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: Colors.grey[800]!),
          ),
          child: const Center(child: Icon(Icons.add, color: Colors.grey)),
        ),
      );
    }
    return DynamicDeckButton(
      button: existing,
      onTap: () => _handlePress(existing, profiles),
      onLongPress: () => _editButton(existing),
    );
  }

  @override
  Widget build(BuildContext context) {
    // Só o configurador web (PC) usa o layout de 3 painéis (perfis | grade | props).
    // No celular mostramos sempre a grade de blocos — deitado ou em pé.
    if (kIsWeb) {
      return SteamDeckDesktopLayout(
        controller: controller,
        onToggleFullscreen: widget.onToggleFullscreen,
        isFullscreen: widget.isFullscreen,
      );
    }

    // Monitor signals
    final isLoading = controller.isLoading.watch(context);
    final error = controller.error.watch(context);
    final profiles = controller.profiles.watch(context);
    final currentProfile = controller.currentProfile.watch(context);

    // Sync PageController with current profile if needed (initial load)
    // Note: Bidirectional sync can be tricky.
    // For now, let PageController drive the selection when swiping.

    // Loading handled via overlay now
    // if (isLoading) {
    //   return const Center(child: CircularProgressIndicator());
    // }

    // If no profiles and no error, show empty state
    if (profiles.isEmpty && error == null) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.grid_off, size: 48, color: Colors.grey),
            const SizedBox(height: 16),
            const Text('No profiles found'),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: controller.loadProfiles,
              child: const Text('Refresh'),
            ),
          ],
        ),
      );
    }

    // Determine initial page index
    int initialIndex = 0;
    if (currentProfile != null) {
      initialIndex = profiles.indexWhere((p) => p.id == currentProfile.id);
      if (initialIndex == -1) initialIndex = 0;
    }

    return Scaffold(
      floatingActionButton: FloatingActionButton(
        onPressed: widget.onToggleFullscreen,
        mini: true,
        backgroundColor: Colors.black45,
        child: Icon(
          widget.isFullscreen ? Icons.fullscreen_exit : Icons.fullscreen,
          color: Colors.white,
        ),
      ),
      appBar: widget.isFullscreen
          ? null
          : AppBar(
              leading: _navStack.isNotEmpty
                  ? IconButton(
                      icon: const Icon(Icons.arrow_back),
                      tooltip: 'Voltar',
                      onPressed: () => _goBack(profiles),
                    )
                  : null,
              title: Text(_navStack.isNotEmpty
                  ? (_folderProfile(profiles)?.name ?? 'Pasta')
                  : (currentProfile?.name ?? 'StreamDeck')),
              actions: [
                // SIGNALR DEBUG INDICATOR
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 8.0),
                  child: Center(
                    child: Builder(builder: (context) {
                      final status = Injector.get<SignalRService>()
                          .connectionStatus
                          .watch(context);
                      Color color = Colors.grey;
                      if (status == "Connected") color = Colors.green;
                      if (status.startsWith("Conn Error")) color = Colors.red;
                      return IconButton(
                        onPressed: () {
                          // Show Debug Info
                          final signalR = Injector.get<SignalRService>();
                          final debugData = signalR.lastDebugData.value;
                          final currentUrl = signalR.currentUrl.value;

                          showDialog(
                            context: context,
                            builder: (ctx) => AlertDialog(
                              title: const Text('Connection Debug'),
                              content: SingleChildScrollView(
                                child: Text(
                                    "Status: $status\n\nURL: $currentUrl\n\nLast Data:\n$debugData"),
                              ),
                              actions: [
                                TextButton(
                                    onPressed: () => Navigator.pop(ctx),
                                    child: const Text('Close'))
                              ],
                            ),
                          );
                        },
                        tooltip: status,
                        icon: Container(
                          width: 14,
                          height: 14,
                          decoration: BoxDecoration(
                            color: color,
                            shape: BoxShape.circle,
                            border: Border.all(color: Colors.white, width: 2),
                            boxShadow: [
                              BoxShadow(
                                color: Colors.black26,
                                blurRadius: 2,
                                offset: Offset(0, 1),
                              )
                            ],
                          ),
                        ),
                      );
                    }),
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.settings),
                  onPressed: () {
                    // Rename Profile Dialog
                    final nameController =
                        TextEditingController(text: currentProfile?.name ?? '');
                    showDialog(
                      context: context,
                      builder: (context) => AlertDialog(
                        title: const Text('Rename Profile'),
                        content: TextField(
                          controller: nameController,
                          decoration:
                              const InputDecoration(labelText: 'Profile Name'),
                        ),
                        actions: [
                          TextButton(
                            onPressed: () => Navigator.pop(context),
                            child: const Text('Cancel'),
                          ),
                          ElevatedButton(
                            onPressed: () {
                              if (currentProfile != null &&
                                  nameController.text.isNotEmpty) {
                                // Update name
                                final updatedProfile = DeckProfile(
                                  id: currentProfile.id,
                                  name: nameController.text,
                                  rows: currentProfile.rows,
                                  columns: currentProfile.columns,
                                  isDefault: currentProfile.isDefault,
                                  buttons: currentProfile.buttons,
                                );
                                controller.updateProfileFull(updatedProfile);
                                Navigator.pop(context);
                              }
                            },
                            child: const Text('Save'),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ],
            ),
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            // A grade é o que cabe na tela (dentro da SafeArea: sem câmera/barra).
            final g = _computeGrid(constraints.maxWidth, constraints.maxHeight);
            _maybeReportLayout(g);
            return Stack(
              children: [
                PageView.builder(
                  controller: _pageController,
                  itemCount: profiles.length,
                  onPageChanged: (index) {
                    if (index >= 0 && index < profiles.length) {
                      controller.currentProfile.value = profiles[index];
                    }
                  },
                  itemBuilder: (context, pageIndex) {
                    return _buildButtonGrid(profiles[pageIndex], profiles, g);
                  },
                ),
                if (error != null)
                  Positioned(
                    bottom: 16,
                    left: 16,
                    right: 16,
                    child: Card(
                      color: Colors.redAccent.withValues(alpha: 0.9),
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          children: [
                            const Icon(Icons.wifi_off, color: Colors.white),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Text(
                                'Offline: $error',
                                style: const TextStyle(color: Colors.white),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                            IconButton(
                              icon:
                                  const Icon(Icons.refresh, color: Colors.white),
                              onPressed: controller.loadProfiles,
                            )
                          ],
                        ),
                      ),
                    ),
                  ),
                if (isLoading)
                  Container(
                    color: Colors.black.withValues(alpha: 0.5),
                    child: const Center(
                      child: CircularProgressIndicator(),
                    ),
                  ),
                // Sobreposição da pasta aberta (perfil empilhado).
                if (_navStack.isNotEmpty && _folderProfile(profiles) != null)
                  Positioned.fill(
                    child: Container(
                      color: Theme.of(context).scaffoldBackgroundColor,
                      child: _buildButtonGrid(
                          _folderProfile(profiles)!, profiles, g),
                    ),
                  ),
              ],
            );
          },
        ),
      ),
    );
  }
}

/// Resultado do cálculo de encaixe da grade na tela.
class _GridFit {
  final int cols;
  final int rows;
  final double cell;
  final double spacing;
  const _GridFit({
    required this.cols,
    required this.rows,
    required this.cell,
    required this.spacing,
  });
}
