import 'package:droiddeck/src/config/pc_switcher.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/stream_deck/layout/desktop_layout.dart';
import 'package:droiddeck/src/studio/companion_surface_page.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:droiddeck/src/stream_deck/layout/grid_placement.dart';
import 'package:droiddeck/src/stream_deck/widgets/connection_chip.dart';
import 'package:droiddeck/src/stream_deck/actions/action_catalog.dart';
import 'package:droiddeck/src/stream_deck/stream_deck_controller.dart';
import 'package:droiddeck/src/stream_deck/widgets/dynamic_deck_button.dart';
import 'package:droiddeck/src/stream_deck/widgets/button_editor_dialog.dart';
import 'package:droiddeck/src/stream_deck/widgets/copy_move_dialog.dart';
import 'package:droiddeck/src/stream_deck/widgets/profile_settings_dialog.dart';
import 'package:droiddeck/core/core.dart';
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

  /// Grade desenhada agora (para achar casa livre ao copiar um botão).
  _GridFit? _grid;

  /// Deck no navegador: usa a grade do celular (guardada no PC) em vez de calcular a
  /// própria — senão o navegador e o celular brigariam pela grade.
  Map<String, int>? _sharedGrid;

  /// Modo de edição: o toque abre o editor em vez de executar. Necessário para editar
  /// botões cujo toque longo já tem uma ação própria.
  bool _editMode = false;

  /// Perfil em que o usuário estava antes de o app em foco trocar de perfil sozinho.
  String? _profileBeforeAuto;

  /// Página mudando por código (não pelo dedo).
  bool _jumping = false;

  /// Último aviso de troca automática já aplicado ('' = nenhum ainda).
  String? _appliedAuto = '';
  void Function()? _autoProfileCleanup;
  void Function()? _reconnectCleanup;
  void Function()? _pageSyncCleanup;

  @override
  void initState() {
    super.initState();
    controller.loadProfiles();
    if (WebMode.deck) _loadSharedGrid();
    // Catálogo de ações: ícones padrão e estados ao vivo (timers, Windows).
    ActionCatalog.instance.load();

    // Sync ao vivo: recarrega os perfis quando o backend avisa que o deck mudou
    // (ex.: alguém editou no configurador web do PC).
    final signalR = Injector.get<SignalRService>();
    var lastSeen = signalR.deckUpdated.value;
    _deckSyncCleanup = effect(() {
      final v = signalR.deckUpdated.value;
      if (v != lastSeen) {
        lastSeen = v;
        controller.loadProfiles();
        if (WebMode.deck) _loadSharedGrid();
      }
    });

    _pageController = PageController();

    // A página acompanha o perfil atual quando ele muda por fora do arrastar (último
    // perfil aberto, troca automática pelo app em foco).
    _pageSyncCleanup = effect(() {
      final current = controller.currentProfile.value;
      final list = controller.profiles.value;
      if (current == null) return;
      final index = list.indexWhere((p) => p.id == current.id);
      untracked(() => _jumpToPage(index));
    });

    // Troca automática: o PC avisa qual perfil pertence ao app em foco. Também roda
    // quando os perfis chegam (o aviso pode vir antes), mas cada aviso só é aplicado
    // uma vez: recarregar o deck não puxa o usuário de volta.
    _autoProfileCleanup = effect(() {
      final auto = signalR.autoProfile.value;
      final enabled = ThemeController.instance.autoProfile.value;
      controller.profiles.value;
      if (!enabled || WebMode.configurator || auto.isEmpty) return;
      final id = auto['profileId'] as String?;
      if (id == _appliedAuto) return;
      untracked(() {
        if (_applyAutoProfile(id)) _appliedAuto = id;
      });
    });

    // Reconectou (PC reiniciado, Wi-Fi voltou): o deck pode ter mudado enquanto a
    // conexão estava fora, e os avisos de mudança se perderam. Recarrega.
    var wasConnected = signalR.connectionStatus.value == 'Connected';
    _reconnectCleanup = effect(() {
      final connected = signalR.connectionStatus.value == 'Connected';
      if (connected && !wasConnected) untracked(controller.loadProfiles);
      wasConnected = connected;
    });
  }

  Future<void> _loadSharedGrid() async {
    final grid = await Injector.get<DroidDeckClient>().getLayout();
    if (grid != null && mounted) setState(() => _sharedGrid = grid);
  }

  void _jumpToPage(int index) {
    if (index < 0) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_pageController.hasClients) return;
      if (_pageController.page?.round() != index) {
        // O jumpToPage dispara onPageChanged: marcado para não parecer um arrastar.
        _jumping = true;
        _pageController.jumpToPage(index);
        _jumping = false;
      }
    });
  }

  /// Aplica um aviso de troca automática. False se os perfis ainda não chegaram.
  bool _applyAutoProfile(String? profileId) {
    final list = controller.profiles.value;
    if (list.isEmpty) return false;
    if (profileId != null) {
      final target = list.where((p) => p.id == profileId).firstOrNull;
      if (target == null) return true;
      final current = controller.currentProfile.value;
      if (current?.id == target.id && _navStack.isEmpty) return true;
      // Guarda para onde voltar só na primeira troca automática da sequência.
      _profileBeforeAuto ??= current?.id;
      setState(() {
        _navStack.clear();
        _rootProfile = null;
      });
      controller.currentProfile.value = target;
      return true;
    } else if (_profileBeforeAuto != null) {
      final back = list.where((p) => p.id == _profileBeforeAuto).firstOrNull;
      _profileBeforeAuto = null;
      if (back != null) {
        setState(() {
          _navStack.clear();
          _rootProfile = null;
        });
        controller.currentProfile.value = back;
      }
    }
    return true;
  }

  @override
  void dispose() {
    _pageSyncCleanup?.call();
    _autoProfileCleanup?.call();
    _reconnectCleanup?.call();
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

  /// Trata um gesto: abre pasta (open_profile), volta (back) ou executa a ação.
  void _handlePress(
    DeckButton b,
    List<DeckProfile> profiles, {
    String gesture = 'tap',
  }) {
    // Botão de monitor ou de informação sem ação: só mostra, o toque não faz nada.
    if (gesture == 'tap' &&
        (b.dynamicType?.isNotEmpty ?? false) &&
        b.action == null &&
        !b.toggle) {
      return;
    }
    final action = b.actionFor(gesture);
    final type = action?.type;
    if (type == 'open_profile') {
      final targetId = action!.parameters['profileId'];
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
    // Telas do estúdio abrem aqui mesmo: quem sabe mostrar é o app.
    if (type == 'companion') {
      Navigator.of(context).push(MaterialPageRoute(builder: (_) => const CompanionSurfacePage()));
      return;
    }
    if (type == 'tally') {
      final op = action!.parameters['operation'] ?? 'open';
      if (op == 'open') {
        Navigator.of(context).push(MaterialPageRoute(
            builder: (_) => TallyPage(target: action.parameters['target'])));
        return;
      }
      // Só mostrar: o sinal manual muda no toque, os outros alvos só se observam.
      if (op == 'show' && action.parameters['target'] != tallyManualTarget) return;
    }
    // Dois estados vai ao PC mesmo sem ação: o toque alterna o estado lá.
    if (action == null && !(gesture == 'tap' && b.toggle)) return;
    final messenger = ScaffoldMessenger.of(context);
    if (controller.offline.value) {
      _offlineNotice(messenger);
      return;
    }
    controller.executeButtonAction(b, gesture: gesture).then((ok) {
      if (!ok) _offlineNotice(messenger);
    });
  }

  void _offlineNotice(ScaffoldMessengerState messenger) {
    messenger
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            tr('O PC não respondeu. Confira se o DroidDeck está aberto nele.'),
          ),
          duration: const Duration(seconds: 3),
        ),
      );
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
          controller.currentProfile.value = profiles.firstWhere(
            (p) => p.id == _navStack.last,
          );
        } catch (_) {}
      }
    });
  }

  void _editButton(DeckButton button) {
    // O diálogo já fechou quando a resposta do PC chega: o aviso usa o messenger da
    // página, não o context do diálogo.
    final messenger = ScaffoldMessenger.of(context);
    showDialog(
      context: context,
      builder: (context) => ButtonEditorDialog(
        button: button,
        onCopyOrMove: button.id.isEmpty
            ? null
            : () {
                Navigator.of(context).pop();
                _copyOrMove(button);
              },
        onSave: (b) async {
          final refused = await controller.updateButton(b);
          // "Limpar botão" apaga na hora, mas dá para desfazer.
          if (refused == null && b.id.isEmpty && button.id.isNotEmpty) {
            messenger.showSnackBar(SnackBar(
              content: Text(tr('Botão removido.')),
              duration: const Duration(seconds: 6),
              // Com ação, o Flutter deixa o aviso na tela até alguém fechar; este some sozinho.
              persist: false,
              action: SnackBarAction(
                label: tr('Desfazer'),
                onPressed: () => controller.updateButton(button),
              ),
            ));
            return;
          }
          if (refused != null) {
            messenger.showSnackBar(
              SnackBar(
                content: Text(refused),
                duration: const Duration(seconds: 6),
              ),
            );
          }
        },
      ),
    );
  }

  /// A grade de quem define o deck (o celular), mesmo copiando pelo navegador.
  void _copyOrMove(DeckButton button) {
    final shared = _sharedGrid;
    final profile = controller.currentProfile.value;
    copyOrMoveButton(
      context,
      controller: controller,
      button: button,
      rows: shared?['rows'] ?? _grid?.rows ?? profile?.rows ?? 3,
      columns: shared?['columns'] ?? _grid?.cols ?? profile?.columns ?? 5,
    );
  }

  /// Calcula quantos botões (cols×rows) cabem INTEIROS no espaço disponível
  /// (já dentro da SafeArea) e o tamanho de cada botão pra preencher sem rolagem.
  _GridFit _computeGrid(double w, double h) {
    const spacing = 8.0;
    const target = 96.0; // tamanho-alvo do botão (confortável pra tocar)
    int cols = ((w + spacing) / (target + spacing)).floor();
    int rows = ((h + spacing) / (target + spacing)).floor();
    // Deck no navegador: a grade é a do celular pareado quando ela cabe com teclas de
    // tamanho decente (tablet, PC); senão o aparelho usa a própria grade e os botões
    // entram em ordem de leitura (ver _buildButtonGrid).
    final shared = _sharedGrid;
    if (shared != null) {
      final sc = shared['columns']!, sr = shared['rows']!;
      final sharedCell = ((w - spacing * (sc - 1)) / sc) < ((h - spacing * (sr - 1)) / sr)
          ? (w - spacing * (sc - 1)) / sc
          : (h - spacing * (sr - 1)) / sr;
      if (sharedCell >= 72) {
        cols = sc;
        rows = sr;
      }
    }
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

  /// Grade cols×rows, centralizada. Botões grandes ocupam várias casas. Num aparelho em
  /// que a grade do celular pareado não cabe (iPhone em pé), os botões são reorganizados
  /// em ordem de leitura na grade deste aparelho, com rolagem se passarem da tela.
  Widget _buildButtonGrid(
    DeckProfile profile,
    List<DeckProfile> profiles,
    _GridFit g,
  ) {
    final shared = _sharedGrid;
    final reflow = WebMode.deck &&
        shared != null &&
        (shared['columns'] != g.cols || shared['rows'] != g.rows);
    final buttons = reflow ? reflowButtons(profile.buttons, g.cols) : profile.buttons;
    // Na grade reorganizada a posição não é a salva: editar usa o botão original.
    final original = {for (final b in profile.buttons) b.id: b};
    var rows = g.rows;
    if (reflow) {
      for (final b in buttons) {
        if (b.row + b.rowSpan > rows) rows = b.row + b.rowSpan;
      }
    }
    final layout = GridLayout.of(buttons, rows, g.cols);
    final step = g.cell + g.spacing;
    double size(int span) => span * g.cell + (span - 1) * g.spacing;
    // Fora do modo de edição as casas vazias não aparecem: centraliza só a área ocupada
    // (um deck de 5 botões numa grade de 10 colunas não fica colado à esquerda).
    var usedCols = g.cols, usedRows = rows;
    if (!_editMode && layout.placements.isNotEmpty) {
      usedCols = 0;
      usedRows = 0;
      for (final p in layout.placements) {
        if (p.column + p.columnSpan > usedCols) usedCols = p.column + p.columnSpan;
        if (p.row + p.rowSpan > usedRows) usedRows = p.row + p.rowSpan;
      }
    }
    final grid = SizedBox(
      width: size(usedCols),
      height: size(usedRows),
      child: Stack(
        children: [
          // Casas vazias: só no modo de edição (ou num deck vazio) e nunca na grade
          // reorganizada, onde a casa não corresponde a uma posição salva.
          if (!reflow)
            for (var row = 0; row < rows; row++)
              for (var col = 0; col < g.cols; col++)
                if (layout.isFree(row, col))
                  Positioned(
                    left: col * step,
                    top: row * step,
                    width: g.cell,
                    height: g.cell,
                    child: _buildCell(row, col, null, profiles,
                        showEmpty: _editMode || profile.buttons.isEmpty,
                        movable: _editMode),
                  ),
          for (final p in layout.placements)
            Positioned(
              left: p.column * step,
              top: p.row * step,
              width: size(p.columnSpan),
              height: size(p.rowSpan),
              child: _buildCell(p.row, p.column, original[p.button.id] ?? p.button, profiles,
                  movable: _editMode && !reflow),
            ),
        ],
      ),
    );
    if (usedRows <= g.rows) return Center(child: grid);
    return SingleChildScrollView(
      padding: EdgeInsets.symmetric(vertical: g.spacing),
      child: Center(child: grid),
    );
  }

  Widget _buildCell(
    int row,
    int col,
    DeckButton? existing,
    List<DeckProfile> profiles, {
    bool showEmpty = true,
    bool movable = false,
  }) {
    final cell = _buildCellContent(row, col, existing, profiles, showEmpty: showEmpty);
    return movable ? _movableCell(row, col, existing, cell) : cell;
  }

  /// Modo de edição: segurar um botão e arrastar para outra casa move (ou troca de lugar
  /// com o que estiver lá), como no configurador do PC.
  Widget _movableCell(int row, int col, DeckButton? existing, Widget cell) {
    return DragTarget<DeckButton>(
      onWillAcceptWithDetails: (d) => d.data.row != row || d.data.column != col,
      onAcceptWithDetails: (d) {
        ThemeController.instance.tapFeedback();
        controller.moveButton(d.data, row, col);
      },
      builder: (context, candidate, _) {
        final scheme = Theme.of(context).colorScheme;
        final target = candidate.isEmpty
            ? cell
            : DecoratedBox(
                position: DecorationPosition.foreground,
                decoration: BoxDecoration(
                  border: Border.all(color: scheme.tertiary, width: 3),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: cell,
              );
        if (existing == null) return target;
        return LayoutBuilder(
          builder: (context, c) => LongPressDraggable<DeckButton>(
            data: existing,
            // Antes do toque longo do botão (500 ms), que abriria o editor.
            delay: const Duration(milliseconds: 350),
            hapticFeedbackOnStart: true,
            feedback: SizedBox(
              width: c.maxWidth,
              height: c.maxHeight,
              child: Material(
                type: MaterialType.transparency,
                child: Opacity(
                  opacity: 0.85,
                  child: DynamicDeckButton(button: existing, onTap: () {}, onLongPress: () {}),
                ),
              ),
            ),
            childWhenDragging: Opacity(opacity: 0.3, child: cell),
            child: target,
          ),
        );
      },
    );
  }

  Widget _buildCellContent(
    int row,
    int col,
    DeckButton? existing,
    List<DeckProfile> profiles, {
    required bool showEmpty,
  }) {
    if (existing == null) {
      // Fora do modo de edição, casa vazia é só espaço: um toque sem querer no meio de
      // uma live não abre "Novo botão". Deck ainda vazio mostra os "+" para começar.
      if (!showEmpty) return const SizedBox.shrink();
      final novo = DeckButton(
        id: '',
        label: tr('Novo botão'),
        row: row,
        column: col,
        action: DeckAction(type: 'none'),
      );
      return Semantics(
        button: true,
        label: tr('Criar botão'),
        child: DeckEmptyTile(
          onTap: () => _editButton(novo),
          onLongPress: () => _editButton(novo),
        ),
      );
    }
    // Toque longo com ação própria executa a ação. Sem ela, editar só no modo de edição
    // (lápis na barra); fora dele, um aviso diz como editar.
    final hasLongPress = existing.longPressAction != null;
    final tile = DynamicDeckButton(
      button: existing,
      liveKeys: !_editMode,
      onTap: _editMode
          ? () => _editButton(existing)
          : () => _handlePress(existing, profiles),
      onLongPress: _editMode
          ? () => _editButton(existing)
          : hasLongPress
              ? () => _handlePress(existing, profiles, gesture: 'longPress')
              : _hintEditMode,
    );
    // Só botões com toque duplo pagam a espera (~300 ms) para diferenciar do simples.
    if (_editMode || existing.doubleTapAction == null) return tile;
    return GestureDetector(
      onDoubleTap: () => _handlePress(existing, profiles, gesture: 'doubleTap'),
      child: tile,
    );
  }

  void _hintEditMode() {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        // Em tela cheia a barra de cima (com o lápis) está escondida.
        content: Text(widget.isFullscreen
            ? tr('Para editar, toque em "Editar agora".')
            : tr('Para editar, toque no lápis da barra de cima.')),
        // Com ação, o Flutter deixa o aviso na tela até alguém fechar; este some sozinho.
        persist: false,
        action: SnackBarAction(
          label: tr('Editar agora'),
          onPressed: () => setState(() => _editMode = true),
        ),
      ));
  }

  @override
  Widget build(BuildContext context) {
    // Só o configurador web (PC) usa o layout de 3 painéis (perfis | grade | props).
    // No celular mostramos sempre a grade de blocos — deitado ou em pé.
    if (WebMode.configurator) {
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
            Icon(
              Icons.grid_off,
              size: 48,
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
            const SizedBox(height: 16),
            Text(tr('Nenhum perfil encontrado')),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: controller.loadProfiles,
              child: Text(tr('Atualizar')),
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

    // Voltar do Android fecha a pasta aberta antes de sair do app.
    return PopScope(
      canPop: _navStack.isEmpty,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _goBack(profiles);
      },
      child: Scaffold(
        backgroundColor: DeckTheme.of(context).background,
        floatingActionButton: FloatingActionButton(
          onPressed: widget.onToggleFullscreen,
          mini: true,
          backgroundColor: Theme.of(
            context,
          ).colorScheme.surfaceContainerHighest.withValues(alpha: 0.7),
          foregroundColor: Theme.of(context).colorScheme.onSurface,
          child: Icon(
            widget.isFullscreen ? Icons.fullscreen_exit : Icons.fullscreen,
          ),
        ),
        appBar: widget.isFullscreen
            ? null
            : AppBar(
                leading: _navStack.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.arrow_back),
                        tooltip: tr('Voltar'),
                        onPressed: () => _goBack(profiles),
                      )
                    : null,
                title: Text(
                  _navStack.isNotEmpty
                      ? (_folderProfile(profiles)?.name ?? tr('Pasta'))
                      : (currentProfile?.name ?? 'DroidDeck'),
                ),
                actions: [
                  const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 4),
                    child: Center(child: ConnectionChip()),
                  ),
                  const PcMenuButton(),
                  IconButton(
                    icon: Icon(_editMode ? Icons.check : Icons.edit),
                    tooltip: _editMode ? tr('Concluir edição') : tr('Editar botões'),
                    onPressed: () => setState(() => _editMode = !_editMode),
                  ),
                  IconButton(
                    icon: const Icon(Icons.tune),
                    tooltip: tr('Perfil'),
                    onPressed: currentProfile == null
                        ? null
                        : () => ProfileSettingsDialog.show(
                            context,
                            currentProfile,
                            controller.updateProfileFull,
                          ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.settings),
                    tooltip: tr('Configurações'),
                    onPressed: () => Navigator.of(context).pushNamed('/config'),
                  ),
                ],
              ),
        body: SafeArea(
          child: LayoutBuilder(
            builder: (context, constraints) {
              // A grade é o que cabe na tela (dentro da SafeArea: sem câmera/barra).
              final g = _computeGrid(
                constraints.maxWidth,
                constraints.maxHeight,
              );
              _maybeReportLayout(g);
              _grid = g;
              return Stack(
                children: [
                  PageView.builder(
                    controller: _pageController,
                    itemCount: profiles.length,
                    onPageChanged: (index) {
                      if (index >= 0 && index < profiles.length) {
                        controller.currentProfile.value = profiles[index];
                        if (!_jumping) {
                          controller.rememberProfile(profiles[index].id);
                        }
                        // Mudou de página na mão: a troca automática não volta mais pra cá.
                        if (!_jumping) _profileBeforeAuto = null;
                      }
                    },
                    itemBuilder: (context, pageIndex) {
                      return _buildButtonGrid(profiles[pageIndex], profiles, g);
                    },
                  ),
                  // Tela cheia esconde a barra (e o selo). Se a conexão cair, o selo volta
                  // no canto: o deck não fica mudo apertando botões que não chegam ao PC.
                  if (widget.isFullscreen) const _FullscreenLinkBadge(),
                  // Acabou de parear e o deck está vazio: diz os dois caminhos para o
                  // primeiro botão em vez de deixar só uma grade de "+".
                  if (error == null &&
                      currentProfile != null &&
                      currentProfile.buttons.isEmpty &&
                      profiles.every((p) => p.buttons.isEmpty))
                    Positioned(
                      bottom: 16,
                      left: 16,
                      right: 16,
                      child: _EmptyDeckHint(
                        onEdit: () => setState(() => _editMode = true),
                        editing: _editMode,
                      ),
                    ),
                  if (error != null)
                    Positioned(
                      bottom: 16,
                      left: 16,
                      right: 16,
                      child: Card(
                        color: Theme.of(context).colorScheme.errorContainer,
                        child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Row(
                            children: [
                              Icon(Icons.wifi_off, color: Theme.of(context).colorScheme.onErrorContainer),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Text(
                                  controller.offline.value
                                      ? tr('PC desconectado. Mostrando o último deck salvo neste aparelho.')
                                      : tr('O PC não respondeu e este aparelho ainda não tem um deck dele salvo. '
                                          'Confira se o DroidDeck está aberto nele e na mesma rede.'),
                                  style: TextStyle(color: Theme.of(context).colorScheme.onErrorContainer),
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              IconButton(
                                icon: Icon(
                                  Icons.refresh,
                                  color: Theme.of(context).colorScheme.onErrorContainer,
                                ),
                                onPressed: controller.loadProfiles,
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  if (isLoading)
                    Container(
                      color: Colors.black.withValues(alpha: 0.5),
                      child: const Center(child: CircularProgressIndicator()),
                    ),
                  // Sobreposição da pasta aberta (perfil empilhado).
                  if (_navStack.isNotEmpty && _folderProfile(profiles) != null)
                    Positioned.fill(
                      child: Container(
                        color: DeckTheme.of(context).background,
                        child: _buildButtonGrid(
                          _folderProfile(profiles)!,
                          profiles,
                          g,
                        ),
                      ),
                    ),
                  if (_editMode)
                    Positioned(
                      top: 0,
                      left: 0,
                      right: 0,
                      child: Center(
                        child: Material(
                          color: Theme.of(context).colorScheme.primary,
                          borderRadius: const BorderRadius.vertical(
                            bottom: Radius.circular(8),
                          ),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 12,
                              vertical: 4,
                            ),
                            child: Text(
                              tr('Modo de edição: toque para editar, segure e arraste para mover'),
                              style: TextStyle(
                                fontSize: 12,
                                color: Theme.of(context).colorScheme.onPrimary,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
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

/// Primeiro uso: o deck ainda não tem nenhum botão em nenhum perfil.
class _EmptyDeckHint extends StatelessWidget {
  final VoidCallback onEdit;
  final bool editing;

  const _EmptyDeckHint({required this.onEdit, required this.editing});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: Card(
          color: scheme.surfaceContainerHigh,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(tr('Conectado! Agora, o primeiro botão'),
                    style: Theme.of(context)
                        .textTheme
                        .titleMedium
                        ?.copyWith(fontWeight: FontWeight.w700)),
                const SizedBox(height: 6),
                Text(
                  tr('Toque num + para criar um botão aqui. Para atalhos e programas, monte '
                      'no PC: clique no ícone do DroidDeck perto do relógio → "Abrir no '
                      'navegador". O que você criar lá aparece aqui na hora.'),
                  style: TextStyle(color: scheme.onSurfaceVariant, height: 1.35),
                ),
                if (!editing) ...[
                  const SizedBox(height: 8),
                  Align(
                    alignment: Alignment.centerRight,
                    child: FilledButton.icon(
                      onPressed: onEdit,
                      icon: const Icon(Icons.edit, size: 18),
                      label: Text(tr('Criar aqui no celular')),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Selo de conexão na tela cheia, só quando não está conectado.
class _FullscreenLinkBadge extends StatelessWidget {
  const _FullscreenLinkBadge();

  @override
  Widget build(BuildContext context) {
    final status = Injector.get<SignalRService>().connectionStatus.watch(context);
    if (status == 'Connected') return const SizedBox.shrink();
    return const Positioned(
      top: 8,
      right: 8,
      child: ConnectionChip(compact: true),
    );
  }
}
