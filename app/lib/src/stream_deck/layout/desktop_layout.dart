import 'grid_placement.dart';
import '../widgets/share_profile_dialog.dart';
import 'package:droiddeck/src/config/triggers_page.dart';
import 'package:droiddeck/src/stream_deck/stream_deck_controller.dart';
import 'package:droiddeck/src/stream_deck/widgets/button_property_panel.dart';
import 'package:droiddeck/src/stream_deck/widgets/copy_move_dialog.dart';
import 'package:droiddeck/src/stream_deck/widgets/dynamic_deck_button.dart';
import 'package:droiddeck/src/stream_deck/widgets/connection_chip.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/config/appearance_settings_page.dart';
import 'package:droiddeck/src/config/integrations_page.dart';
import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/stream_deck/widgets/profile_settings_dialog.dart';
import 'package:droiddeck/src/stream_deck/backup/backup_buttons.dart';
import 'package:flutter/material.dart';

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
  void initState() {
    super.initState();
    // Aberto pelo link de um perfil compartilhado (site → localhost:4787/?import=...).
    WidgetsBinding.instance.addPostFrameCallback((_) {
      importSharedProfileFromUrl(context, (id) async {
        await widget.controller.loadProfiles();
        final p = widget.controller.profiles.value.where((p) => p.id == id).firstOrNull;
        if (p != null) widget.controller.currentProfile.value = p;
      });
    });
  }

  /// Chave do painel aberto. Muda junto com o botão selecionado: o painel guarda o
  /// formulário no próprio estado (lido só ao abrir), então trocar de botão com a mesma
  /// chave deixava o formulário do botão anterior na tela.
  var _panelKey = GlobalKey<ButtonPropertyPanelState>();
  String? _panelFor;

  GlobalKey<ButtonPropertyPanelState> _keyFor(DeckButton b) {
    final id = b.id.isEmpty ? 'novo:${b.row}:${b.column}' : b.id;
    if (id != _panelFor) {
      _panelFor = id;
      _panelKey = GlobalKey<ButtonPropertyPanelState>();
    }
    return _panelKey;
  }

  /// Abaixo disso o painel de propriedades vira uma folha por cima da grade.
  static const _widePanel = 1100.0;

  @override
  Widget build(BuildContext context) {
    final profiles = widget.controller.profiles.watch(context);
    final currentProfile = widget.controller.currentProfile.watch(context);
    final isLoading = widget.controller.isLoading.watch(context);

    if (isLoading && profiles.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: LayoutBuilder(builder: (context, c) {
        final wide = c.maxWidth >= _widePanel;
        final panel = _buildPanel(profiles, overlay: !wide);
        return Row(
          children: [
            _buildSidebar(profiles, currentProfile),
            Expanded(
              child: Container(
                color: DeckTheme.of(context).background,
                child: Stack(
                  children: [
                    Column(
                      children: [
                        _buildTopBar(currentProfile),
                        Expanded(
                          child: currentProfile == null
                              ? Center(child: Text(tr('Nenhum perfil selecionado')))
                              : _buildDeckGrid(currentProfile),
                        ),
                      ],
                    ),
                    // Tela estreita: o painel aparece por cima, só com um botão escolhido.
                    if (!wide && _selectedButton != null)
                      Positioned(
                        top: 0,
                        bottom: 0,
                        right: 0,
                        width: 380,
                        child: Material(
                          elevation: 12,
                          color: scheme.surfaceContainer,
                          child: panel,
                        ),
                      ),
                  ],
                ),
              ),
            ),
            if (wide)
              Container(
                width: 360,
                decoration: BoxDecoration(
                  color: scheme.surfaceContainer,
                  border: Border(left: BorderSide(color: scheme.outlineVariant)),
                ),
                child: panel,
              ),
          ],
        );
      }),
    );
  }

  Widget _buildSidebar(List<DeckProfile> profiles, DeckProfile? currentProfile) {
    final scheme = Theme.of(context).colorScheme;
    Widget tool(IconData icon, String label, VoidCallback onTap) => Padding(
          padding: const EdgeInsets.fromLTRB(8, 0, 8, 2),
          child: TextButton.icon(
            onPressed: onTap,
            icon: Icon(icon, size: 18),
            label: Text(label),
            style: TextButton.styleFrom(
              alignment: Alignment.centerLeft,
              minimumSize: const Size(double.infinity, 38),
              foregroundColor: scheme.onSurfaceVariant,
            ),
          ),
        );
    return Container(
      width: 240,
      decoration: BoxDecoration(
        color: scheme.surfaceContainer,
        border: Border(right: BorderSide(color: scheme.outlineVariant)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 18, 16, 8),
            child: Text(tr('Perfis'),
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: scheme.onSurfaceVariant, fontWeight: FontWeight.w600)),
          ),
          Expanded(
            child: ListView.builder(
              itemCount: profiles.length,
              itemBuilder: (context, index) {
                final profile = profiles[index];
                final isSelected = profile.id == currentProfile?.id;
                return ListTile(
                  selected: isSelected,
                  selectedTileColor: scheme.primary.withValues(alpha: 0.14),
                  selectedColor: scheme.onSurface,
                  title: Text(profile.name,
                      style: TextStyle(fontWeight: isSelected ? FontWeight.w600 : null)),
                  // Excluir fica num menu: um clique errado na linha não apaga nada.
                  trailing: PopupMenuButton<String>(
                    tooltip: tr('Opções do perfil'),
                    icon: Icon(Icons.more_vert, size: 20, color: scheme.onSurfaceVariant),
                    onSelected: (v) {
                      if (v == 'settings') _renameProfile(profile);
                      if (v == 'share') ShareProfileDialog.show(context, profile);
                      if (v == 'delete') _deleteProfile(profile);
                    },
                    itemBuilder: (_) => [
                      PopupMenuItem(value: 'settings', child: Text(tr('Nome e troca automática'))),
                      PopupMenuItem(value: 'share', child: Text(tr('Compartilhar'))),
                      PopupMenuItem(
                          value: 'delete',
                          child: Text(tr('Excluir perfil'), style: TextStyle(color: scheme.error))),
                    ],
                  ),
                  onTap: () => _guardDirty(() {
                    widget.controller.switchProfile(profile.id);
                    setState(() => _selectedButton = null);
                  }),
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
            // Criar perfil é secundário aqui (a tarefa é montar botões): contorno, não amarelo.
            child: OutlinedButton.icon(
              onPressed: _createNewProfile,
              icon: const Icon(Icons.add),
              label: Text(tr('Novo perfil')),
              style: OutlinedButton.styleFrom(minimumSize: const Size(double.infinity, 42)),
            ),
          ),
          Divider(height: 1, color: scheme.outlineVariant),
          const SizedBox(height: 6),
          tool(Icons.extension_outlined, tr('Integrações'), () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const IntegrationsPage()))),
          tool(Icons.bolt, tr('Automações'), () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const TriggersPage()))),
          tool(Icons.palette_outlined, tr('Aparência'), () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const AppearanceSettingsPage()))),
          const SizedBox(height: 6),
          Divider(height: 1, color: scheme.outlineVariant),
          BackupButtons(
            dense: true,
            onImported: widget.controller.loadProfiles,
          ),
          const SizedBox(height: 6),
        ],
      ),
    );
  }

  /// Nome do perfil, o estado da conexão e as ações do perfil, com rótulos.
  Widget _buildTopBar(DeckProfile? currentProfile) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(24, 16, 16, 0),
      child: Row(
        children: [
          Expanded(
            child: Text(
              currentProfile?.name ?? '',
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
            ),
          ),
          if (currentProfile != null)
            TextButton.icon(
              onPressed: () => _renameProfile(currentProfile),
              icon: const Icon(Icons.tune, size: 18),
              label: Text(tr('Editar perfil')),
            ),
          const SizedBox(width: 8),
          const ConnectionChip(),
          IconButton(
            icon: Icon(widget.isFullscreen ? Icons.fullscreen_exit : Icons.fullscreen),
            tooltip: widget.isFullscreen ? tr('Sair da tela cheia') : tr('Tela cheia'),
            onPressed: widget.onToggleFullscreen,
          ),
        ],
      ),
    );
  }

  /// Painel do botão escolhido, ou um guia curto quando nada está escolhido.
  Widget _buildPanel(List<DeckProfile> profiles, {required bool overlay}) {
    final selected = _selectedButton;
    if (selected == null) {
      final muted = Theme.of(context).colorScheme.onSurfaceVariant;
      Widget step(IconData icon, String text) => Padding(
            padding: const EdgeInsets.only(bottom: 14),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(icon, size: 20, color: Theme.of(context).colorScheme.primary),
                const SizedBox(width: 12),
                Expanded(child: Text(text, style: TextStyle(color: muted, height: 1.4))),
              ],
            ),
          );
      return Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(tr('Montando o deck'),
                style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
            const SizedBox(height: 16),
            step(Icons.add_box_outlined, tr('Clique num espaço vazio da grade para criar um botão.')),
            step(Icons.touch_app_outlined, tr('Clique num botão para mudar a ação, o ícone ou a cor.')),
            step(Icons.open_with, tr('Arraste um botão para outro lugar para mover ou trocar.')),
            step(Icons.phone_android,
                tr('Tudo aparece no celular na hora. A grade tem o tamanho que cabe na tela dele.')),
          ],
        ),
      );
    }
    return ButtonPropertyPanel(
      key: _keyFor(selected),
      button: selected,
      profiles: profiles,
      onSave: (btn) {
        if (btn.id.isEmpty) {
          _clearWithUndo(selected);
        } else {
          widget.controller.updateButton(btn);
        }
        setState(() => _selectedButton = btn.id.isEmpty ? null : btn);
      },
      onCancel: () => setState(() => _selectedButton = null),
      onCopyOrMove: () {
        final lay = Injector.get<SignalRService>().deckLayout.value;
        copyOrMoveButton(
          context,
          controller: widget.controller,
          button: selected,
          rows: lay['rows'] ?? 3,
          columns: lay['columns'] ?? 5,
        );
      },
    );
  }

  /// "Limpar botão" apaga na hora, mas dá para desfazer.
  void _clearWithUndo(DeckButton previous) {
    widget.controller.updateButton(DeckButton(
        id: '', row: previous.row, column: previous.column, label: '', backgroundColor: ''));
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(tr('Botão removido.')),
      duration: const Duration(seconds: 6),
      // Com ação, o Flutter deixa o aviso na tela até alguém fechar; este some sozinho.
      persist: false,
      action: SnackBarAction(
        label: tr('Desfazer'),
        onPressed: () => widget.controller.updateButton(previous),
      ),
    ));
  }

  /// Antes de trocar de botão ou de perfil: com mudanças não salvas, pergunta.
  Future<void> _guardDirty(VoidCallback proceed) async {
    if (_panelKey.currentState?.isDirty != true) {
      proceed();
      return;
    }
    final discard = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(tr('Descartar as alterações?')),
        content: Text(tr('O botão tem mudanças que ainda não foram salvas.')),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: Text(tr('Continuar editando'))),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: Text(tr('Descartar'))),
        ],
      ),
    );
    if (discard == true) proceed();
  }

  Widget _buildDeckGrid(DeckProfile profile) {
    // A grade é definida pelo celular (quantos botões cabem na tela) e reportada
    // ao PC. Aqui só exibimos essa grade, do maior tamanho que couber.
    final lay = Injector.get<SignalRService>().deckLayout.watch(context);
    final cols = (lay['columns'] ?? 5);
    final rows = (lay['rows'] ?? 3);
    return LayoutBuilder(builder: (context, c) {
      const spacing = 12.0;
      const pad = 24.0;
      final availW = c.maxWidth - pad * 2;
      final availH = c.maxHeight - pad * 2 - 28; // legenda embaixo
      final byW = (availW - spacing * (cols - 1)) / cols;
      final byH = (availH - spacing * (rows - 1)) / rows;
      final cell = (byW < byH ? byW : byH).clamp(48.0, 150.0);
      final step = cell + spacing;
      double size(int span) => span * cell + (span - 1) * spacing;
      final layout = GridLayout.of(profile.buttons, rows, cols);
      return SingleChildScrollView(
        padding: const EdgeInsets.all(pad),
        child: Center(
          child: Column(
            children: [
              SizedBox(
                width: size(cols),
                height: size(rows),
                child: Stack(
                  children: [
                    for (var row = 0; row < rows; row++)
                      for (var col = 0; col < cols; col++)
                        if (layout.isFree(row, col))
                          Positioned(
                            left: col * step,
                            top: row * step,
                            width: cell,
                            height: cell,
                            child: _buildSlot(row, col, null),
                          ),
                    // Botões grandes ocupam várias casas.
                    for (final p in layout.placements)
                      Positioned(
                        left: p.column * step,
                        top: p.row * step,
                        width: size(p.columnSpan),
                        height: size(p.rowSpan),
                        child: _buildSlot(p.row, p.column, p.button),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Text(
                tr('Grade do celular: {cols} × {rows}. Arraste um botão para mover ou trocar.',
                    {'cols': cols, 'rows': rows}),
                style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant, fontSize: 13),
              ),
            ],
          ),
        ),
      );
    });
  }

  Widget _buildSlot(int row, int col, DeckButton? existing) {
    final scheme = Theme.of(context).colorScheme;
    final isSelectedPos =
        _selectedButton?.row == row && _selectedButton?.column == col;

    void select(DeckButton b) {
      if (_selectedButton?.row == b.row && _selectedButton?.column == b.column) return;
      _guardDirty(() => setState(() => _selectedButton = b));
    }

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
                ? scheme.tertiary
                : (isSelectedPos ? scheme.primary : Colors.transparent),
            width: 3,
          ),
          borderRadius: BorderRadius.circular(12),
        );

        // Slot vazio: a própria célula seleciona/cria um botão novo.
        if (existing == null) {
          return Semantics(
            button: true,
            label: tr('Criar botão'),
            child: InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: () => select(
                  DeckButton(id: '', row: row, column: col, label: '', action: null)),
              child: Container(
                decoration: decoration,
                child: const DeckEmptyTile(),
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

  void _createNewProfile() {
    // Basic dialog for new profile
    final controller = TextEditingController();
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(tr('Novo perfil')),
        content: TextField(
          controller: controller,
          decoration: InputDecoration(labelText: tr('Nome do perfil')),
          autofocus: true,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: Text(tr('Cancelar'))),
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
            child: Text(tr('Criar')),
          ),
        ],
      ),
    );
  }

  void _deleteProfile(DeckProfile profile) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(tr('Excluir perfil?')),
        content: Text(tr(
            'Excluir "{name}"? Não dá para desfazer.', {'name': profile.name})),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx), child: Text(tr('Cancelar'))),
          TextButton(
            onPressed: () {
              widget.controller.deleteProfile(profile.id);
              Navigator.pop(ctx);
            },
            style: TextButton.styleFrom(foregroundColor: Colors.red),
            child: Text(tr('Excluir')),
          ),
        ],
      ),
    );
  }

  void _renameProfile(DeckProfile? profile) {
    if (profile == null) return;
    ProfileSettingsDialog.show(
        context, profile, widget.controller.updateProfileFull);
  }
}
