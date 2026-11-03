import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../theme/deck_theme.dart';
import '../../theme/theme_controller.dart';

/// Moldura de um botão do deck, no estilo do tema atual. O conteúdo (ícone,
/// rótulo) herda a cor certa via IconTheme/DefaultTextStyle — leia com
/// [DeckTile.foregroundOf] quando precisar dela explicitamente.
class DeckTile extends StatelessWidget {
  const DeckTile({
    super.key,
    required this.child,
    this.color,
    this.active = false,
    this.activeColor,
    this.onTap,
    this.onLongPress,
  });

  final Widget child;

  /// Cor escolhida para o botão; nula usa a do tema.
  final Color? color;

  /// Toggle ligado (mute, gravando, luz acesa...).
  final bool active;

  /// Cor de "ligado"; nula usa a do tema.
  final Color? activeColor;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;

  static Color foregroundOf(BuildContext context) =>
      IconTheme.of(context).color ?? Colors.white;

  @override
  Widget build(BuildContext context) {
    final deck = DeckTheme.of(context);
    final on = activeColor ?? deck.activeColor;
    // Nos estilos de traço, cor neutra (o cinza padrão do editor) não tem o que
    // destacar: fica como "sem cor" e usa o branco/traço do tema.
    final accent =
        color != null && HSLColor.fromColor(color!).saturation >= 0.15
        ? color
        : null;

    final Widget tile;
    final Color fg;
    switch (deck.tileStyle) {
      case DeckTileStyle.solid:
        final fill = active ? on : (color ?? deck.tileColor);
        fg = fill.computeLuminance() > 0.55 ? Colors.black87 : Colors.white;
        tile = Container(
          decoration: BoxDecoration(
            color: fill,
            borderRadius: BorderRadius.circular(8),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.3),
                blurRadius: 4,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          clipBehavior: Clip.antiAlias,
          child: _content(fg, const EdgeInsets.all(4)),
        );
      case DeckTileStyle.outline:
        final tint = legibleOn(
          active ? on : (accent ?? deck.outline),
          deck.background,
        );
        fg = active
            ? deck.foreground
            : (accent != null ? tint : deck.foreground);
        tile = Container(
          decoration: BoxDecoration(
            color: Color.alphaBlend(
              tint.withValues(alpha: active ? 0.35 : 0.08),
              deck.tileColor,
            ),
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: tint, width: 2),
          ),
          clipBehavior: Clip.antiAlias,
          child: _content(fg, const EdgeInsets.all(4)),
        );
      case DeckTileStyle.frame:
        final tint = accent != null
            ? legibleOn(accent, deck.background)
            : deck.outline;
        final activeTint = legibleOn(on, deck.background);
        fg = accent != null ? tint : deck.foreground;
        tile = LayoutBuilder(
          builder: (context, c) {
            final side = math.min(c.maxWidth, c.maxHeight);
            return CustomPaint(
              painter: _FramePainter(
                outline: deck.outline,
                fill: active
                    ? Color.alphaBlend(
                        activeTint.withValues(alpha: 0.30),
                        deck.tileColor,
                      )
                    : deck.tileColor,
                innerStroke: active ? activeTint : deck.outline,
                corner: active ? activeTint : tint,
                radius: _FramePainter.outerRadius(side),
              ),
              child: _content(fg, EdgeInsets.all(side * 0.12)),
            );
          },
        );
    }

    if (onTap == null && onLongPress == null) return tile;
    return _Pressable(onTap: onTap, onLongPress: onLongPress, child: tile);
  }

  Widget _content(Color fg, EdgeInsets padding) {
    return IconTheme.merge(
      data: IconThemeData(color: fg),
      child: DefaultTextStyle.merge(
        style: TextStyle(color: fg),
        child: Padding(
          padding: padding,
          child: Center(child: child),
        ),
      ),
    );
  }

  /// Ajusta a luminosidade para a cor continuar visível sobre o fundo: um botão
  /// cinza-escuro vira cinza-claro num tema escuro, por exemplo.
  static Color legibleOn(Color c, Color background) {
    final hsl = HSLColor.fromColor(c);
    if (background.computeLuminance() < 0.3) {
      return hsl.lightness < 0.62 ? hsl.withLightness(0.62).toColor() : c;
    }
    return hsl.lightness > 0.42 ? hsl.withLightness(0.42).toColor() : c;
  }
}

/// Botão que afunda ao encostar o dedo (e vibra): sem vibração, o afundar é a única
/// confirmação de que o toque pegou.
class _Pressable extends StatefulWidget {
  const _Pressable({required this.child, this.onTap, this.onLongPress});

  final Widget child;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;

  @override
  State<_Pressable> createState() => _PressableState();
}

class _PressableState extends State<_Pressable> {
  bool _down = false;

  void _set(bool down) {
    if (_down != down) setState(() => _down = down);
  }

  @override
  Widget build(BuildContext context) {
    final reduce = MediaQuery.maybeDisableAnimationsOf(context) ?? false;
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      // Vibra ao encostar, não ao soltar: é o que dá a sensação de botão físico.
      onTapDown: (_) {
        _set(true);
        if (widget.onTap != null) ThemeController.instance.tapFeedback();
      },
      onTapUp: (_) => _set(false),
      onTapCancel: () => _set(false),
      onLongPressEnd: (_) => _set(false),
      onTap: widget.onTap,
      onLongPress: widget.onLongPress,
      child: AnimatedScale(
        scale: _down ? 0.93 : 1,
        duration: Duration(milliseconds: reduce ? 0 : (_down ? 60 : 140)),
        curve: Curves.easeOutCubic,
        child: AnimatedOpacity(
          opacity: _down ? 0.82 : 1,
          duration: Duration(milliseconds: reduce ? 0 : 90),
          child: widget.child,
        ),
      ),
    );
  }
}

/// Slot sem botão. Toque cria um botão novo.
class DeckEmptyTile extends StatelessWidget {
  const DeckEmptyTile({super.key, this.onTap, this.onLongPress});

  final VoidCallback? onTap;
  final VoidCallback? onLongPress;

  @override
  Widget build(BuildContext context) {
    final deck = DeckTheme.of(context);
    final frame = deck.tileStyle == DeckTileStyle.frame;
    final add = Icon(Icons.add, color: deck.outline.withValues(alpha: 0.25));

    final Widget tile;
    if (frame) {
      tile = LayoutBuilder(
        builder: (context, c) {
          final side = math.min(c.maxWidth, c.maxHeight);
          return Container(
            decoration: BoxDecoration(
              color: deck.emptyTileColor,
              borderRadius: BorderRadius.circular(
                _FramePainter.outerRadius(side),
              ),
              border: Border.all(
                color: deck.outline.withValues(alpha: 0.6),
                width: _FramePainter.outerStroke(side),
              ),
            ),
            child: Center(child: add),
          );
        },
      );
    } else {
      final outline = deck.tileStyle == DeckTileStyle.outline;
      tile = Container(
        decoration: BoxDecoration(
          color: deck.emptyTileColor,
          borderRadius: BorderRadius.circular(outline ? 10 : 8),
          border: Border.all(
            color: deck.outline.withValues(alpha: outline ? 0.25 : 0.12),
            width: outline ? 1.5 : 1,
          ),
        ),
        child: Center(child: add),
      );
    }

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: onTap,
      onLongPress: onLongPress,
      child: tile,
    );
  }
}

/// Moldura externa fina + moldura interna grossa com o canto inferior direito
/// chanfrado, e um triângulo de cor encaixado nesse canto.
class _FramePainter extends CustomPainter {
  _FramePainter({
    required this.outline,
    required this.fill,
    required this.innerStroke,
    required this.corner,
    required this.radius,
  });

  final Color outline;
  final Color fill;
  final Color innerStroke;
  final Color corner;
  final double radius;

  static double outerRadius(double side) => side * 0.12;
  static double outerStroke(double side) => math.max(1.2, side * 0.015);

  @override
  void paint(Canvas canvas, Size size) {
    final side = math.min(size.width, size.height);
    final outer = Offset.zero & size;
    final ow = outerStroke(side);

    canvas.drawRRect(
      RRect.fromRectAndRadius(outer.deflate(ow / 2), Radius.circular(radius)),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = ow
        ..color = outline.withValues(alpha: 0.6),
    );

    final iw = math.max(2.0, side * 0.03);
    final inner = outer.deflate(side * 0.08 + iw / 2);
    final r = side * 0.08;
    final c = side * 0.26; // chanfro
    final t = side * 0.15; // triângulo
    final l = inner.left,
        top = inner.top,
        right = inner.right,
        b = inner.bottom;

    final path = Path()
      ..moveTo(l + r, top)
      ..lineTo(right - r, top)
      ..arcToPoint(Offset(right, top + r), radius: Radius.circular(r))
      ..lineTo(right, b - c)
      ..lineTo(right - c, b)
      ..lineTo(l + r, b)
      ..arcToPoint(Offset(l, b - r), radius: Radius.circular(r))
      ..lineTo(l, top + r)
      ..arcToPoint(Offset(l + r, top), radius: Radius.circular(r))
      ..close();

    canvas.drawPath(path, Paint()..color = fill);
    // Brilho leve atrás do traço: dá o ar de tela iluminada.
    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = iw * 2
        ..color = innerStroke.withValues(alpha: 0.18)
        ..maskFilter = MaskFilter.blur(BlurStyle.normal, iw),
    );
    canvas.drawPath(
      path,
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = iw
        ..strokeJoin = StrokeJoin.round
        ..color = innerStroke,
    );

    final tri = Path()
      ..moveTo(right + iw / 2, b - t)
      ..lineTo(right + iw / 2, b + iw / 2)
      ..lineTo(right - t, b + iw / 2)
      ..close();
    canvas.drawPath(tri, Paint()..color = corner);
  }

  @override
  bool shouldRepaint(_FramePainter old) =>
      old.outline != outline ||
      old.fill != fill ||
      old.innerStroke != innerStroke ||
      old.corner != corner ||
      old.radius != radius;
}
