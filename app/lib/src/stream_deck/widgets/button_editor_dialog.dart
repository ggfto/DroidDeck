import 'package:droiddeck/src/stream_deck/widgets/button_property_panel.dart';
import 'package:droiddeck/core/core.dart';
import 'package:flutter/material.dart';

class ButtonEditorDialog extends StatelessWidget {
  final DeckButton button;
  final Function(DeckButton) onSave;

  /// Ver [ButtonPropertyPanel.onCopyOrMove].
  final VoidCallback? onCopyOrMove;

  const ButtonEditorDialog({
    super.key,
    required this.button,
    required this.onSave,
    this.onCopyOrMove,
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
          onCopyOrMove: onCopyOrMove,
        ),
      ),
    );
  }
}
