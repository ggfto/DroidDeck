import 'package:companion/src/stream_deck/widgets/button_property_panel.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';

class ButtonEditorDialog extends StatelessWidget {
  final DeckButton button;
  final Function(DeckButton) onSave;

  const ButtonEditorDialog({
    super.key,
    required this.button,
    required this.onSave,
  });

  @override
  Widget build(BuildContext context) {
    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 500, maxHeight: 700),
        child: ButtonPropertyPanel(
          button: button,
          onSave: (btn) {
            onSave(btn);
            Navigator.of(context).pop();
          },
          onCancel: () => Navigator.of(context).pop(),
        ),
      ),
    );
  }
}
