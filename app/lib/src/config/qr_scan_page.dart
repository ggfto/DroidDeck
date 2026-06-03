import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

/// Tela de leitura do QR de pareamento. Retorna (via Navigator.pop) a string
/// crua do QR (ex.: "droiddeck://pair?ip=..&port=..&key=..") quando reconhece um.
class QrScanPage extends StatefulWidget {
  const QrScanPage({super.key});

  @override
  State<QrScanPage> createState() => _QrScanPageState();
}

class _QrScanPageState extends State<QrScanPage> {
  bool _handled = false;

  void _onDetect(BarcodeCapture capture) {
    if (_handled) return;
    for (final barcode in capture.barcodes) {
      final value = barcode.rawValue;
      if (value != null && value.startsWith('droiddeck://')) {
        _handled = true;
        Navigator.of(context).pop(value);
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Parear — aponte para o QR')),
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
          const Positioned(
            bottom: 40,
            child: Text(
              'Abra "Parear dispositivo (QR)" na bandeja do PC',
              style: TextStyle(
                color: Colors.white,
                backgroundColor: Colors.black54,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
