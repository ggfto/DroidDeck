import 'package:companion/src/stream_deck/stream_deck_controller.dart';
import 'package:companion/src/stream_deck/widgets/button_property_panel.dart';
import 'package:companion/src/stream_deck/widgets/dynamic_deck_button.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'package:collection/collection.dart';

class SteamDeckDesktopLayout extends StatefulWidget {
  final StreamDeckController controller;
  final VoidCallback onToggleFullscreen;
  final bool isFullscreen;

  const SteamDeckDesktopLayout({
    super.key,
    required this.controller,
    required this.onToggleFullscreen,
    required this.isFullscreen,
  });

  @override
  State<SteamDeckDesktopLayout> createState() => _SteamDeckDesktopLayoutState();
}

class _SteamDeckDesktopLayoutState extends State<SteamDeckDesktopLayout> {
  DeckButton? _selectedButton;

  @override
  Widget build(BuildContext context) {
    final profiles = widget.controller.profiles.watch(context);
    final currentProfile = widget.controller.currentProfile.watch(context);
    final isLoading = widget.controller.isLoading.watch(context);

    if (isLoading && profiles.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    return Scaffold(
      body: Row(
        children: [
          // Left Sidebar: Profiles
          Container(
            width: 250,
            decoration: BoxDecoration(
              color: Colors.grey[900],
              border: Border(right: BorderSide(color: Colors.white10)),
            ),
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: Text('PROFILES',
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                          color: Colors.grey, fontWeight: FontWeight.bold)),
                ),
                Expanded(
                  child: ListView.builder(
                    itemCount: profiles.length,
                    itemBuilder: (context, index) {
                      final profile = profiles[index];
                      final isSelected = profile.id == currentProfile?.id;
                      return ListTile(
                        selected: isSelected,
                        selectedTileColor:
                            Theme.of(context).primaryColor.withValues(alpha: 0.2),
                        title: Text(profile.name),
                        trailing: IconButton(
                          icon: const Icon(Icons.delete,
                              size: 20, color: Colors.white54),
                          tooltip: 'Delete Profile',
                          onPressed: () => _deleteProfile(profile),
                        ),
                        onTap: () {
                          widget.controller.switchProfile(profile.id);
                          setState(() {
                            _selectedButton = null;
                          });
                        },
                      );
                    },
                  ),
                ),
                Divider(color: Colors.white10),
                Padding(
                  padding: const EdgeInsets.all(8.0),
                  child: ElevatedButton.icon(
                    onPressed: _createNewProfile,
                    icon: Icon(Icons.add),
                    label: Text('New Profile'),
                    style: ElevatedButton.styleFrom(
                      minimumSize: Size(double.infinity, 40),
                    ),
                  ),
                ),
              ],
            ),
          ),

          // Center: Grid
          Expanded(
            child: Container(
              color: Colors.black87,
              child: Stack(
                children: [
                  Center(
                    child: currentProfile == null
                        ? const Text('No Profile Selected')
                        : _buildDeckGrid(currentProfile),
                  ),
                  Positioned(
                    top: 16,
                    right: 16,
                    child: Row(
                      children: [
                        // SIGNALR DEBUG INDICATOR
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 8.0),
                          child: Builder(builder: (context) {
                            final status = Injector.get<SignalRService>()
                                .connectionStatus
                                .watch(context);
                            final debugData = Injector.get<SignalRService>()
                                .lastDebugData
                                .watch(context); // Watch debug signal
                            Color color = Colors.grey;
                            if (status == "Connected") color = Colors.green;
                            if (status.startsWith("Conn Error"))
                              color = Colors.red;
                            return Tooltip(
                              message: "$status\n$debugData", // Show Data
                              child: Container(
                                width: 12,
                                height: 12,
                                decoration: BoxDecoration(
                                    color: color, shape: BoxShape.circle),
                              ),
                            );
                          }),
                        ),
                        IconButton(
                          icon: Icon(Icons.edit),
                          tooltip: 'Rename Profile',
                          onPressed: () => _renameProfile(currentProfile),
                        ),
                        IconButton(
                          icon: Icon(widget.isFullscreen
                              ? Icons.fullscreen_exit
                              : Icons.fullscreen),
                          onPressed: widget.onToggleFullscreen,
                        ),
                      ],
                    ),
                  )
                ],
              ),
            ),
          ),

          // Right Sidebar: Properties
          Container(
            width: 350,
            decoration: BoxDecoration(
              color: Colors.grey[900],
              border: Border(left: BorderSide(color: Colors.white10)),
            ),
            child: _selectedButton == null
                ? const Center(child: Text('Select a button to edit'))
                : ButtonPropertyPanel(
                    key: ValueKey(_selectedButton!
                        .id), // Force rebuild on selection change
                    button: _selectedButton!,
                    profiles: profiles,
                    onSave: (btn) {
                      widget.controller.updateButton(btn);
                      // Update local selection to reflect changes
                      setState(() {
                        _selectedButton = btn;
                      });
                    },
                    onCancel: () {
                      setState(() {
                        _selectedButton = null;
                      });
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildDeckGrid(DeckProfile profile) {
    final cols = profile.columns > 0 ? profile.columns : 5;
    final rows = profile.rows > 0 ? profile.rows : 3;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(32),
      child: Column(
        children: [
          _buildGridSizeBar(profile, rows, cols),
          const SizedBox(height: 24),
          Center(
            child: SizedBox(
              width: 800, // Constrain width for desktop
              child: GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: cols,
                  crossAxisSpacing: 16,
                  mainAxisSpacing: 16,
                  childAspectRatio: 1.0,
                ),
                itemCount: rows * cols,
                itemBuilder: (context, index) {
                  final row = index ~/ cols;
                  final col = index % cols;
                  final existingButton = profile.buttons.firstWhereOrNull(
                    (b) => b.row == row && b.column == col,
                  );
                  return _buildSlot(row, col, existingButton);
                },
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSlot(int row, int col, DeckButton? existing) {
    final isSelectedPos =
        _selectedButton?.row == row && _selectedButton?.column == col;

    void select(DeckButton b) => setState(() => _selectedButton = b);

    return DragTarget<DeckButton>(
      onWillAcceptWithDetails: (d) => d.data.row != row || d.data.column != col,
      onAcceptWithDetails: (d) {
        widget.controller.moveButton(d.data, row, col);
        setState(() => _selectedButton = null);
      },
      builder: (context, candidate, rejected) {
        final highlight = candidate.isNotEmpty;
        final decoration = BoxDecoration(
          border: Border.all(
            color: highlight
                ? Colors.greenAccent
                : (isSelectedPos ? Colors.blueAccent : Colors.transparent),
            width: 3,
          ),
          borderRadius: BorderRadius.circular(12),
        );

        // Slot vazio: a própria célula (InkWell) seleciona/cria um botão novo.
        if (existing == null) {
          return InkWell(
            onTap: () => select(
                DeckButton(id: '', row: row, column: col, label: '', action: null)),
            child: Container(
              decoration: decoration,
              child: Container(
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.05),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.white10),
                ),
                child: const Icon(Icons.add, color: Colors.white24),
              ),
            ),
          );
        }

        // Botão existente: o PRÓPRIO botão trata o toque (seleciona para editar),
        // já que ele tem gesto interno. Fica arrastável para mover/trocar.
        final buttonWidget = Container(
          decoration: decoration,
          child: DynamicDeckButton(
            button: existing,
            onTap: () => select(existing),
            onLongPress: () => select(existing),
          ),
        );

        return Draggable<DeckButton>(
          data: existing,
          feedback: SizedBox(
            width: 90,
            height: 90,
            child: Opacity(
              opacity: 0.85,
              child: DynamicDeckButton(
                  button: existing, onTap: () {}, onLongPress: () {}),
            ),
          ),
          childWhenDragging: Opacity(opacity: 0.3, child: buttonWidget),
          child: buttonWidget,
        );
      },
    );
  }

  Widget _buildGridSizeBar(DeckProfile profile, int rows, int cols) {
    return Wrap(
      alignment: WrapAlignment.center,
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 16,
      children: [
        const Text('Grade:', style: TextStyle(color: Colors.white70)),
        _stepper(
            'Linhas', rows, (v) => widget.controller.setGridSize(v, cols)),
        _stepper(
            'Colunas', cols, (v) => widget.controller.setGridSize(rows, v)),
        const Text('arraste um botão para mover/trocar',
            style: TextStyle(color: Colors.white30, fontSize: 12)),
      ],
    );
  }

  Widget _stepper(String label, int value, ValueChanged<int> onChanged) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('$label ', style: const TextStyle(color: Colors.white54)),
        IconButton(
          icon: const Icon(Icons.remove_circle_outline, size: 20),
          onPressed: value > 1 ? () => onChanged(value - 1) : null,
        ),
        Text('$value',
            style: const TextStyle(
                color: Colors.white, fontWeight: FontWeight.bold)),
        IconButton(
          icon: const Icon(Icons.add_circle_outline, size: 20),
          onPressed: value < 10 ? () => onChanged(value + 1) : null,
        ),
      ],
    );
  }

  void _createNewProfile() {
    // Basic dialog for new profile
    final controller = TextEditingController();
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('New Profile'),
        content: TextField(
          controller: controller,
          decoration: InputDecoration(labelText: 'Profile Name'),
          autofocus: true,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: Text('Cancel')),
          ElevatedButton(
            onPressed: () {
              if (controller.text.isNotEmpty) {
                // Create profile logic
                // We don't have createProfile in controller yet, mimicking app.js logic
                // Ideally controller should handle this.
                // For now, let's just make a dummy profile and save it if controller allows.
                final newProfile = DeckProfile(
                  id: DateTime.now().millisecondsSinceEpoch.toString(),
                  name: controller.text,
                  rows: 3,
                  columns: 5,
                );
                widget.controller.updateProfileFull(
                    newProfile); // This acts as create/update
                Navigator.pop(ctx);
              }
            },
            child: Text('Create'),
          ),
        ],
      ),
    );
  }

  void _deleteProfile(DeckProfile profile) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Delete Profile?'),
        content: Text(
            'Are you sure you want to delete "${profile.name}"? This action cannot be undone.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          TextButton(
            onPressed: () {
              widget.controller.deleteProfile(profile.id);
              Navigator.pop(ctx);
            },
            style: TextButton.styleFrom(foregroundColor: Colors.red),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
  }

  void _renameProfile(DeckProfile? profile) {
    if (profile == null) return;
    final controller = TextEditingController(text: profile.name);
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Rename Profile'),
        content: TextField(
          controller: controller,
          decoration: InputDecoration(labelText: 'Profile Name'),
          autofocus: true,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: Text('Cancel')),
          ElevatedButton(
            onPressed: () {
              if (controller.text.isNotEmpty) {
                final updated = DeckProfile(
                    id: profile.id,
                    name: controller.text,
                    rows: profile.rows,
                    columns: profile.columns,
                    buttons: profile.buttons,
                    isDefault: profile.isDefault);
                widget.controller.updateProfileFull(updated);
                Navigator.pop(ctx);
              }
            },
            child: Text('Rename'),
          ),
        ],
      ),
    );
  }
}
