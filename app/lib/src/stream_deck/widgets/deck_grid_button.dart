import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:flutter/material.dart';

import '../buttons/button_look.dart';
import '../buttons/renderers.dart';
import 'timer_deck_button.dart';

/// Um botão do deck. O visual de cada tipo de ação fica no renderer dele
/// (`buttons/renderers.dart`); aqui só se escolhe o renderer e se trata o toque.
class DeckGridButton extends StatefulWidget {
  final DeckButton button;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  /// Tecla em modo "segurar" aperta no PC enquanto o dedo estiver no botão. Só no deck
  /// em uso: no modo de edição e no configurador o toque seleciona o botão.
  final bool liveKeys;

  const DeckGridButton({
    super.key,
    required this.button,
    required this.onTap,
    required this.onLongPress,
    this.liveKeys = false,
  });

  @override
  State<DeckGridButton> createState() => _DeckGridButtonState();
}

class _DeckGridButtonState extends State<DeckGridButton> {
  late ButtonLook _look;

  /// Dedo que está segurando a tecla (um segundo dedo no mesmo botão não conta).
  int? _pointer;

  /// O aperta foi pelo hub; sem conexão, o soltar vira um toque pelo /press.
  bool _sentLive = false;

  /// Aperta/solta ao vivo: ação "key" em modo "segurar" e o corte de tosse do Voicemeeter.
  bool get _holdKey {
    if (!widget.liveKeys) return false;
    final action = widget.button.action;
    if (action == null) return false;
    if (action.type == 'voicemeeter') return action.parameters['operation'] == 'hold_mute';
    if (!const {'key', 'keyboard_key', 'send_key'}.contains(action.type)) return false;
    final mode = action.parameters['mode'] ?? 'hold';
    return mode.isEmpty || mode == 'hold';
  }

  ButtonLook _makeLook() => ButtonLook(
        button: widget.button,
        onTap: _handleTap,
        onLongPress: widget.onLongPress,
      );

  @override
  void initState() {
    super.initState();
    _look = _makeLook();
    rendererFor(widget.button.action?.type).init(_look);
  }

  @override
  void didUpdateWidget(DeckGridButton old) {
    super.didUpdateWidget(old);
    _look = _makeLook();
    // Botão trocado (edição, perfil novo) com o dedo em cima: solta a tecla antiga.
    if (old.button.id != widget.button.id || !_holdKey) _release(old.button.id);
  }

  @override
  void dispose() {
    _release(widget.button.id);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // Timer/pomodoro/contador: relógio que anda sozinho, com widget próprio.
    if (widget.button.action?.type == 'timer') {
      return TimerDeckButton(
        button: widget.button,
        onTap: widget.onTap,
        onLongPress: widget.onLongPress,
        color: _look.color,
        activeColor: _look.activeColor,
      );
    }
    final renderer = rendererFor(widget.button.action?.type);
    // O renderer lê os signals de estado dentro deste Watch.
    final tile = Watch((_) => renderer.build(_look));
    if (!_holdKey) return tile;

    // Ponteiro cru em vez de toque: o reconhecedor de toque desiste aos 500 ms (o toque
    // longo vence) e soltava a seta no meio. O IgnorePointer tira os gestos do botão;
    // o afundar e a vibração ficam aqui.
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    final down = _pointer != null;
    return Listener(
      behavior: HitTestBehavior.opaque,
      onPointerDown: _press,
      onPointerUp: (e) => _lift(e.pointer),
      onPointerCancel: (e) => _lift(e.pointer),
      child: IgnorePointer(
        child: AnimatedScale(
          scale: down ? 0.93 : 1,
          duration: Duration(milliseconds: reduce ? 0 : (down ? 60 : 140)),
          curve: Curves.easeOutCubic,
          child: AnimatedOpacity(
            opacity: down ? 0.82 : 1,
            duration: Duration(milliseconds: reduce ? 0 : 90),
            child: tile,
          ),
        ),
      ),
    );
  }

  void _press(PointerDownEvent e) {
    if (_pointer != null) return;
    setState(() => _pointer = e.pointer);
    ThemeController.instance.tapFeedback();
    try {
      _sentLive = Injector.get<SignalRService>().holdKey(widget.button.id);
    } catch (_) {
      _sentLive = false;
    }
  }

  void _lift(int pointer) {
    if (_pointer != pointer) return;
    setState(() => _pointer = null);
    if (_sentLive) {
      _release(widget.button.id);
    } else {
      // Sem o hub (reconectando): vai como toque rápido pelo caminho normal, que também
      // avisa se o PC não responder.
      widget.onTap();
    }
  }

  void _release(String buttonId) {
    if (!_sentLive) return;
    _sentLive = false;
    _pointer = null;
    try {
      Injector.get<SignalRService>().releaseKey(buttonId);
    } catch (_) {}
  }

  /// Atualiza o estado na hora do toque (o PC confirma depois) e executa.
  void _handleTap() {
    try {
      final sr = Injector.get<SignalRService>();
      if (widget.button.toggle) {
        // Dois estados: inverte na hora; o PC confirma pelo ReceiveButtonToggles.
        final cur = Map<String, dynamic>.from(sr.stateOf('ReceiveButtonToggles'))
          ..remove('_receivedAt');
        if (cur[widget.button.id] == true) {
          cur.remove(widget.button.id);
        } else {
          cur[widget.button.id] = true;
        }
        sr.seedState('ReceiveButtonToggles', cur);
      }
      rendererFor(widget.button.action?.type).optimisticTap(_look, sr);
    } catch (_) {}
    widget.onTap();
  }
}
