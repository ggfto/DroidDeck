import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

/// Tela genérica de leitura de QR. Retorna (via Navigator.pop) a string crua do
/// primeiro QR cujo conteúdo começa com um dos [acceptPrefixes]. Usada tanto pro
/// pareamento (`droiddeck://...`) quanto pra ler o QR do OBS (`obsws://...`).
class QrScanPage extends StatefulWidget {
  /// Prefixos aceitos (case-insensitive). Padrão: QR de pareamento.
  final List<String> acceptPrefixes;
  final String title;
  final String hint;

  const QrScanPage({
    super.key,
    this.acceptPrefixes = const ['droiddeck://'],
    this.title = 'Parear — aponte para o QR',
    this.hint = 'Abra "Parear dispositivo (QR)" na bandeja do PC',
  });

  @override
  State<QrScanPage> createState() => _QrScanPageState();
}

class _QrScanPageState extends State<QrScanPage> {
  bool _handled = false;

  void _onDetect(BarcodeCapture capture) {
    if (_handled) return;
    for (final barcode in capture.barcodes) {
      final value = barcode.rawValue;
      if (value == null) continue;
      final lower = value.toLowerCase();
      final ok = widget.acceptPrefixes.any((p) => lower.startsWith(p.toLowerCase()));
      if (ok) {
        _handled = true;
        Navigator.of(context).pop(value);
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      body: Stack(
        alignment: Alignment.center,
        children: [
          MobileScanner(onDetect: _onDetect),
          // Moldura simples de mira
          Container(
            width: 240,
            height: 240,
            decoration: BoxDecoration(
              border: Border.all(color: Colors.yellow, width: 3),
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          Positioned(
            bottom: 40,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              color: Colors.black54,
              child: Text(
                widget.hint,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
