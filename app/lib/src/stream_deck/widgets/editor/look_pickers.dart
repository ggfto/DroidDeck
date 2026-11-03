import 'dart:convert';
import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:droiddeck/core/core.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../buttons/button_icons.dart';

/// Maior GIF aceito como ícone (vai inteiro no perfil, que trafega a cada mudança).
const maxGifBytes = 2 * 1024 * 1024;

/// Abre a galeria e devolve a imagem escolhida em base64, ou null. Fotos são reduzidas
/// para até 512×512 em PNG; GIF animado vai como está (reduzir perderia a animação).
/// Lança [FormatException] para GIF grande demais.
Future<String?> pickButtonImage() async {
  final image = await ImagePicker().pickImage(source: ImageSource.gallery);
  if (image == null) return null;
  final bytes = await image.readAsBytes();
  final isGif = bytes.length > 4 &&
      bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38; // GIF8
  if (isGif) {
    if (bytes.length > maxGifBytes) {
      throw FormatException(tr('GIF grande demais (máximo de 2 MB).'));
    }
    return base64Encode(bytes);
  }
  return base64Encode(await _shrink(bytes, 512));
}

/// Reduz para caber em [max]×[max], em PNG. Imagem menor (ou que não decodifica) fica igual.
Future<Uint8List> _shrink(Uint8List bytes, int max) async {
  try {
    final probe = await ui.instantiateImageCodec(bytes);
    final frame = await probe.getNextFrame();
    final w = frame.image.width, h = frame.image.height;
    frame.image.dispose();
    if (w <= max && h <= max) return bytes;
    final scale = max / (w > h ? w : h);
    final codec = await ui.instantiateImageCodec(bytes,
        targetWidth: (w * scale).round(), targetHeight: (h * scale).round());
    final small = (await codec.getNextFrame()).image;
    final data = await small.toByteData(format: ui.ImageByteFormat.png);
    small.dispose();
    return data == null ? bytes : data.buffer.asUint8List();
  } catch (_) {
    return bytes;
  }
}

/// Grade com os ícones embutidos; o primeiro quadrado é "sem ícone".
class IconGrid extends StatelessWidget {
  final String? selected;
  final ValueChanged<String?> onSelect;
  final double spacing;

  const IconGrid({
    super.key,
    required this.selected,
    required this.onSelect,
    this.spacing = 12,
  });

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    Widget option(String? name) {
      final isSelected = selected == name;
      return InkWell(
        onTap: () => onSelect(name),
        child: Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: isSelected
                ? scheme.primary.withValues(alpha: 0.3)
                : scheme.surfaceContainerHighest,
            borderRadius: BorderRadius.circular(4),
            border: Border.all(color: isSelected ? scheme.primary : scheme.outlineVariant),
          ),
          child: name == null
              ? Icon(Icons.block, color: scheme.onSurfaceVariant)
              : Icon(buttonIconData(name), color: scheme.onSurface),
        ),
      );
    }

    Widget grid(Iterable<String?> names) => Wrap(
          spacing: spacing,
          runSpacing: spacing,
          children: names.map(option).toList(),
        );

    // Por assunto: com mais de 200 ícones, uma grade só virava caça ao tesouro.
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        grid(const [null]),
        for (final (title, names) in iconCategories) ...[
          Padding(
            padding: EdgeInsets.only(top: spacing + 4, bottom: spacing / 2),
            child: Text(
              tr(title),
              style: Theme.of(context).textTheme.labelMedium?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ),
          grid(names),
        ],
      ],
    );
  }
}

/// Prévia de uma imagem escolhida para o botão, com o "remover".
class ImagePreview extends StatelessWidget {
  final String base64;
  final VoidCallback onRemove;
  final double size;

  const ImagePreview({
    super.key,
    required this.base64,
    required this.onRemove,
    this.size = 80,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        children: [
          Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              border: Border.all(color: Theme.of(context).colorScheme.primary),
              borderRadius: BorderRadius.circular(8),
              color: Colors.black12,
            ),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.memory(
                base64Decode(base64),
                fit: BoxFit.contain,
                errorBuilder: (_, __, ___) => const Icon(Icons.error, color: Colors.red),
              ),
            ),
          ),
          TextButton(
            onPressed: onRemove,
            style: TextButton.styleFrom(foregroundColor: Colors.redAccent),
            child: Text(tr('Remover imagem')),
          ),
        ],
      ),
    );
  }
}

/// Cores prontas para o fundo e para o estado ativo.
class ColorRow extends StatelessWidget {
  static final List<Color> palette = [
    Colors.grey[800]!,
    Colors.red[900]!,
    Colors.blue[900]!,
    Colors.green[900]!,
    Colors.orange[900]!,
    Colors.purple[900]!,
    Colors.black,
  ];

  final Color selected;
  final ValueChanged<Color> onSelect;

  const ColorRow({super.key, required this.selected, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      children: palette
          .map((c) => InkWell(
                onTap: () => onSelect(c),
                child: Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: c,
                    shape: BoxShape.circle,
                    border: selected == c ? Border.all(color: Colors.white, width: 2) : null,
                    boxShadow: const [BoxShadow(blurRadius: 2, color: Colors.black26)],
                  ),
                ),
              ))
          .toList(),
    );
  }
}

/// Como o botão fica desligado x ativo, com as cores escolhidas e o estilo do tema.
class ActivePreview extends StatelessWidget {
  final Color color;
  final Color activeColor;

  /// (ícone desligado, ícone ativo).
  final (IconData, IconData) icons;

  const ActivePreview({
    super.key,
    required this.color,
    required this.activeColor,
    required this.icons,
  });

  /// Ícones da prévia conforme a ação — espelha o deck.
  static (IconData, IconData) iconsFor(DeckAction? action) => switch (action?.type) {
        'discord' => action?.parameters['operation'] == 'toggleDeafen'
            ? (Icons.headset_mic, Icons.headset_off)
            : (Icons.mic, Icons.mic_off),
        'obs' => (Icons.fiber_manual_record_outlined, Icons.fiber_manual_record),
        'tuya' || 'homeassistant' => (Icons.lightbulb_outline, Icons.lightbulb),
        'timer' => (Icons.hourglass_bottom, Icons.alarm),
        'windows' => (Icons.headphones, Icons.headphones),
        _ => (Icons.volume_up, Icons.volume_off),
      };

  @override
  Widget build(BuildContext context) {
    final muted = Theme.of(context).colorScheme.onSurfaceVariant;
    Widget chip(String label, bool active, IconData icon) => Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 56,
              height: 56,
              child: DeckTile(
                color: color,
                active: active,
                activeColor: activeColor,
                child: Icon(icon, size: 26),
              ),
            ),
            const SizedBox(height: 4),
            Text(label, style: TextStyle(fontSize: 11, color: muted)),
          ],
        );

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        color: DeckTheme.of(context).background,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceEvenly,
        children: [
          chip(tr('Desligado'), false, icons.$1),
          Icon(Icons.arrow_forward, color: muted, size: 20),
          chip(tr('Ativo'), true, icons.$2),
        ],
      ),
    );
  }
}
