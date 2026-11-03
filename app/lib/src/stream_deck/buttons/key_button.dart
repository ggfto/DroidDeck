import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:flutter/material.dart';

import 'button_look.dart';

/// Tecla do teclado: setas, F-keys, navegação e modificadores com ícones ou rótulos adequados.
class KeyButtonRenderer extends ButtonRenderer {
  const KeyButtonRenderer();

  @override
  Widget build(ButtonLook look) {
    final key = (look.params['key'] ?? '').trim().toUpperCase();
    final IconData? fallbackIcon;
    final String fallbackLabel;

    switch (key) {
      case 'UP':
      case 'ARROWUP':
      case 'UPARROW':
        fallbackIcon = Icons.arrow_upward;
        fallbackLabel = '▲';
        break;
      case 'DOWN':
      case 'ARROWDOWN':
      case 'DOWNARROW':
        fallbackIcon = Icons.arrow_downward;
        fallbackLabel = '▼';
        break;
      case 'LEFT':
      case 'ARROWLEFT':
      case 'LEFTARROW':
        fallbackIcon = Icons.arrow_back;
        fallbackLabel = '◄';
        break;
      case 'RIGHT':
      case 'ARROWRIGHT':
      case 'RIGHTARROW':
        fallbackIcon = Icons.arrow_forward;
        fallbackLabel = '►';
        break;
      case 'ENTER':
      case 'RETURN':
        fallbackIcon = Icons.keyboard_return;
        fallbackLabel = 'Enter';
        break;
      case 'BACKSPACE':
      case 'BKSP':
      case 'BS':
        fallbackIcon = Icons.backspace;
        fallbackLabel = 'Bksp';
        break;
      case 'TAB':
        fallbackIcon = Icons.keyboard_tab;
        fallbackLabel = 'Tab';
        break;
      case 'ESC':
      case 'ESCAPE':
        fallbackIcon = null;
        fallbackLabel = 'Esc';
        break;
      case 'SPACE':
        fallbackIcon = Icons.space_bar;
        fallbackLabel = 'Space';
        break;
      default:
        fallbackIcon = null;
        fallbackLabel = key.isNotEmpty ? key : 'Key';
    }

    final iconWidget = look.hasOwnIcon
        ? look.icon()
        : (fallbackIcon != null ? Icon(fallbackIcon) : const SizedBox.shrink());

    return look.tile(
      active: _latched(look),
      icon: iconWidget,
      label: look.ownLabel ?? fallbackLabel,
    );
  }

  static bool _isLatch(ButtonLook look) =>
      const {'toggle', 'oneshot'}.contains(look.params['mode']);

  /// Tecla travada apertada. O PC guarda no liga/desliga do botão (ReceiveButtonToggles),
  /// mesmo sem "dois estados" ligado nele.
  static bool _latched(ButtonLook look) {
    if (!_isLatch(look)) return false;
    try {
      return Injector.get<SignalRService>().stateOf('ReceiveButtonToggles')[look.button.id] == true;
    } catch (_) {
      return false;
    }
  }

  @override
  void optimisticTap(ButtonLook look, SignalRService signalR) {
    // Com "dois estados" o DeckGridButton já inverteu.
    if (!_isLatch(look) || look.button.toggle) return;
    final cur = Map<String, dynamic>.from(signalR.stateOf('ReceiveButtonToggles'))
      ..remove('_receivedAt');
    if (cur[look.button.id] == true) {
      cur.remove(look.button.id);
    } else {
      cur[look.button.id] = true;
    }
    signalR.seedState('ReceiveButtonToggles', cur);
  }
}
