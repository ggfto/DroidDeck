import 'dart:convert';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:flutter/material.dart';

import '../actions/action_catalog.dart';
import 'button_icons.dart';
import 'colors.dart';

/// Como um tipo de ação desenha o botão no deck. Cada tipo com visual próprio (OBS,
/// Discord, Spotify...) tem o seu; os demais usam o [DefaultButtonRenderer].
///
/// Para um tipo novo: escreva um renderer e registre em `renderers.dart`.
abstract class ButtonRenderer {
  const ButtonRenderer();

  /// Desenha o botão. Roda dentro de um Watch: ler um signal aqui já redesenha quando
  /// ele muda.
  Widget build(ButtonLook look);

  /// Busca o estado inicial, se o tipo precisar (chamado uma vez por botão).
  void init(ButtonLook look) {}

  /// Atualiza o estado local na hora do toque, antes da confirmação do PC, para o
  /// botão não parecer travado. O PC corrige pelo broadcast depois.
  void optimisticTap(ButtonLook look, SignalRService signalR) {}
}

/// Tudo que um renderer precisa de um botão: dados, cores e o [tile] que monta o visual
/// com o estado ativo (inclusive o dos botões de dois estados).
class ButtonLook {
  final DeckButton button;
  final VoidCallback onTap;
  final VoidCallback onLongPress;

  ButtonLook({
    required this.button,
    required this.onTap,
    required this.onLongPress,
  });

  DeckAction? get action => button.action;
  String? get type => action?.type;
  Map<String, String> get params => action?.parameters ?? const {};

  /// Operação da ação, em minúsculas.
  String op(String fallback) => (params['operation'] ?? fallback).toLowerCase();

  Color? get color => parseHexColor(button.backgroundColor);
  Color? get activeColor => parseHexColor(button.activeColor);

  /// Rótulo escolhido pelo usuário (null se vazio).
  String? get ownLabel =>
      (button.label?.isNotEmpty ?? false) ? button.label : null;

  bool get hasOwnIcon =>
      (button.iconName?.isNotEmpty ?? false) ||
      (button.iconBase64?.isNotEmpty ?? false);

  /// Liga/desliga do botão de dois estados (guardado no PC).
  bool get toggledOn {
    if (!button.toggle) return false;
    try {
      return Injector.get<SignalRService>().stateOf(
            'ReceiveButtonToggles',
          )[button.id] ==
          true;
    } catch (_) {
      return false;
    }
  }

  /// O ícone escolhido pelo usuário; sem ele, [fallback] ou o ícone do tipo de ação.
  Widget icon({IconData? fallback, double? size}) {
    if (button.iconBase64?.isNotEmpty ?? false)
      return base64Icon(button.iconBase64!);
    if (button.iconName?.isNotEmpty ?? false) {
      return Icon(buttonIconData(button.iconName!), size: size);
    }
    if (fallback != null) return Icon(fallback, size: size);
    final schema = ActionCatalog.instance.find(type);
    if (schema != null && type != 'hotkey')
      return Icon(schema.iconData, size: size);
    return Builder(
      builder: (context) => Icon(
        Icons.touch_app,
        size: size,
        color: DeckTile.foregroundOf(context).withValues(alpha: 0.54),
      ),
    );
  }

  /// Monta o botão no estado certo: ativo pelo estado da ação ou pelo liga/desliga do
  /// botão. Ativo, o rótulo e o ícone do estado ativo (se definidos) substituem os normais.
  Widget tile({
    required bool active,
    required Widget icon,
    String? label,
    Color? activeColor,
  }) {
    final on = active || toggledOn;
    if (on && (button.activeIconBase64?.isNotEmpty ?? false)) {
      icon = base64Icon(button.activeIconBase64!);
    } else if (on && (button.activeIconName?.isNotEmpty ?? false)) {
      icon = Icon(buttonIconData(button.activeIconName!));
    }
    final text = on && (button.activeLabel?.isNotEmpty ?? false)
        ? button.activeLabel
        : label;
    // Leitor de tela: botão de dois estados diz "ligado/desligado"; os outros dizem
    // só quando o estado está ativo (ex.: microfone mudo), sem inventar um "desligado".
    return Semantics(
      toggled: button.toggle ? on : (on ? true : null),
      child: DeckTile(
        color: color,
        active: on,
        activeColor: activeColor ?? this.activeColor,
        onTap: onTap,
        onLongPress: onLongPress,
        // Rótulo e ícone crescem com o botão: num tablet ou no configurador o texto não
        // fica miúdo; num celular pequeno não estoura.
        child: LayoutBuilder(
          builder: (context, c) {
            final side = c.maxWidth.isFinite && c.maxHeight.isFinite
                ? (c.maxWidth < c.maxHeight ? c.maxWidth : c.maxHeight)
                : 80.0;
            // Regra do Relance: palavra nunca quebra no meio. Se a mais comprida não cabe na
            // largura da tecla, a fonte encolhe (até 9) antes de o texto ser cortado.
            final fontSize = text == null || !c.maxWidth.isFinite
                ? (side * 0.13).clamp(10.0, 16.0)
                : fitWordsFontSize(
                    text,
                    (side * 0.13).clamp(10.0, 16.0),
                    c.maxWidth - 8,
                    MediaQuery.textScalerOf(context),
                  );
            final iconSize = (side * 0.36).clamp(20.0, 48.0);
            return Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                IconTheme.merge(
                  data: IconThemeData(size: iconSize),
                  child: icon,
                ),
                if (text != null && text.isNotEmpty) ...[
                  SizedBox(height: side * 0.04),
                  Text(
                    text,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: fontSize,
                      fontWeight: FontWeight.w600,
                      height: 1.15,
                      overflow: TextOverflow.ellipsis,
                    ),
                    maxLines: 2,
                  ),
                ],
              ],
            );
          },
        ),
      ),
    );
  }

  /// Imagem quadrada que ocupa boa parte do botão (capa de jogo, de álbum).
  static Widget cover(
    String base64, {
    double fraction = 0.78,
    IconData fallback = Icons.image,
  }) {
    return Flexible(
      child: LayoutBuilder(
        builder: (context, c) {
          // Botão grande (2×1) não é quadrado: a imagem não passa da altura disponível.
          final w = c.maxWidth.isFinite ? c.maxWidth * fraction : 64.0;
          final side = c.maxHeight.isFinite && c.maxHeight < w
              ? c.maxHeight
              : w;
          return ClipRRect(
            borderRadius: BorderRadius.circular(6),
            child: SizedBox(
              width: side,
              height: side,
              child: Image.memory(
                base64Decode(stripDataUri(base64)),
                fit: BoxFit.cover,
                gaplessPlayback: true,
                errorBuilder: (_, __, ___) => Icon(fallback),
              ),
            ),
          );
        },
      ),
    );
  }

  static Widget base64Icon(String data) {
    try {
      return Image.memory(
        base64Decode(stripDataUri(data)),
        width: 32,
        height: 32,
        errorBuilder: (_, __, ___) =>
            const Icon(Icons.error, color: Colors.red),
      );
    } catch (_) {
      return const Icon(Icons.broken_image);
    }
  }

  /// Tira o "data:image/...;base64," da frente, quando vem.
  static String stripDataUri(String data) =>
      data.startsWith('data:') ? data.substring(data.indexOf(',') + 1) : data;
}

/// Maior tamanho (até [preferred], no mínimo 9) em que a palavra mais longa de [text]
/// cabe em [maxWidth] numa linha só, em negrito médio (w600).
double fitWordsFontSize(
  String text,
  double preferred,
  double maxWidth,
  TextScaler scaler,
) {
  if (maxWidth <= 0) return preferred;
  final words = text.split(RegExp(r'\s+')).where((w) => w.isNotEmpty);
  double widest = 0;
  for (final w in words) {
    final tp = TextPainter(
      text: TextSpan(
        text: w,
        style: TextStyle(fontSize: preferred, fontWeight: FontWeight.w600),
      ),
      textDirection: TextDirection.ltr,
      textScaler: scaler,
      maxLines: 1,
    )..layout();
    if (tp.width > widest) widest = tp.width;
  }
  if (widest <= maxWidth) return preferred;
  return (preferred * maxWidth / widest).clamp(9.0, preferred);
}
