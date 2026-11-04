import 'dart:convert';
import 'dart:typed_data';

import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/stream_deck/buttons/colors.dart';
import 'package:flutter/material.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

const companionStateEvent = 'ReceiveCompanionState';
const companionKeysEvent = 'ReceiveCompanionKeys';

/// Uma tecla como o Companion mandou desenhar. A imagem fica decodificada aqui: uma troca de
/// página no Companion muda dezenas de teclas de uma vez.
class CompanionKey {
  final int key;
  final Uint8List? image;
  final Color? color;
  final Color? textColor;
  final String? text;
  final bool pressed;

  const CompanionKey({
    required this.key,
    this.image,
    this.color,
    this.textColor,
    this.text,
    this.pressed = false,
  });

  factory CompanionKey.fromJson(Map<dynamic, dynamic> j) {
    Uint8List? image;
    final raw = j['image'];
    if (raw is String && raw.isNotEmpty) {
      try {
        image = base64Decode(raw);
      } catch (_) {
        // Imagem quebrada: a tecla fica só com a cor e o texto.
      }
    }
    return CompanionKey(
      key: (j['key'] as num).toInt(),
      image: image,
      color: parseHexColor(j['color'] as String?),
      textColor: parseHexColor(j['textColor'] as String?),
      text: j['text'] as String?,
      pressed: j['pressed'] == true,
    );
  }
}

/// A superfície do Companion em tela cheia. A grade é a escolhida na configuração da integração.
class CompanionSurfacePage extends StatefulWidget {
  const CompanionSurfacePage({super.key});

  @override
  State<CompanionSurfacePage> createState() => _CompanionSurfacePageState();
}

class _CompanionSurfacePageState extends State<CompanionSurfacePage> {
  final Map<int, CompanionKey> _keys = {};
  int _columns = 8;
  int _rows = 4;
  bool _loaded = false;
  void Function()? _batches;
  void Function()? _reload;

  SignalRService get _signalR => Injector.get<SignalRService>();

  @override
  void initState() {
    super.initState();
    _signalR.listenState(companionStateEvent);
    _signalR.listenState(companionKeysEvent);
    WakelockPlus.toggle(enable: true).catchError((_) {});

    Map<String, dynamic>? lastBatch;
    _batches = effect(() {
      final batch = _signalR.stateOf(companionKeysEvent);
      if (identical(batch, lastBatch) || batch.isEmpty) return;
      lastBatch = batch;
      untracked(() => _applyBatch(batch));
    });

    // Conectou de novo (hub ou Companion): as teclas podem ter mudado no intervalo.
    Object? lastLink;
    _reload = effect(() {
      final hub = _signalR.connectionStatus.value;
      final state = _signalR.stateOf(companionStateEvent);
      final link = '$hub|${state['connected']}|${state['columns']}|${state['rows']}';
      if (link == lastLink) return;
      lastLink = link;
      untracked(_load);
    });
  }

  @override
  void dispose() {
    _batches?.call();
    _reload?.call();
    WakelockPlus.toggle(enable: false).catchError((_) {});
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final j = Map<String, dynamic>.from(
          await Injector.get<DroidDeckClient>().getJson('/api/companion/keys') as Map);
      if (!mounted) return;
      setState(() {
        _columns = (j['columns'] as num?)?.toInt() ?? _columns;
        _rows = (j['rows'] as num?)?.toInt() ?? _rows;
        _keys
          ..clear()
          ..addEntries((j['keys'] as List? ?? const [])
              .map((k) => CompanionKey.fromJson(k as Map))
              .map((k) => MapEntry(k.key, k)));
        _loaded = true;
      });
    } catch (_) {
      if (mounted) setState(() => _loaded = true);
    }
  }

  void _applyBatch(Map<String, dynamic> batch) {
    if (!mounted) return;
    setState(() {
      if (batch['clear'] == true) _keys.clear();
      for (final raw in batch['keys'] as List? ?? const []) {
        final k = CompanionKey.fromJson(raw as Map);
        _keys[k.key] = k;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Watch((context) {
      final connected = _signalR.stateOf(companionStateEvent)['connected'] == true &&
          _signalR.connectionStatus.value == 'Connected';
      return Scaffold(
        backgroundColor: Colors.black,
        appBar: !connected
            ? AppBar(
                title: const Text('Companion'),
                backgroundColor: Colors.black,
                foregroundColor: Colors.white,
              )
            : null,
        body: !_loaded
            ? const Center(child: CircularProgressIndicator())
            : !connected
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Text(
                        tr('O PC não está conectado ao Companion. Configure em Integrações → Companion.'),
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.white70),
                      ),
                    ),
                  )
                : SafeArea(
                    child: Column(
                      children: [
                        Expanded(child: _grid()),
                        // Faixa propria: um botao por cima cobriria uma tecla.
                        SizedBox(
                          height: 36,
                          child: Align(
                            alignment: Alignment.centerRight,
                            child: IconButton(
                              iconSize: 20,
                              padding: EdgeInsets.zero,
                              color: Colors.white70,
                              tooltip: tr('Voltar'),
                              icon: const Icon(Icons.close),
                              onPressed: () => Navigator.of(context).maybePop(),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
      );
    });
  }

  Widget _grid() {
    const gap = 6.0;
    return LayoutBuilder(builder: (context, c) {
      final cellW = (c.maxWidth - gap * (_columns - 1)) / _columns;
      final cellH = (c.maxHeight - gap * (_rows - 1)) / _rows;
      final cell = (cellW < cellH ? cellW : cellH).clamp(1.0, double.infinity);
      return Center(
        child: SizedBox(
          width: cell * _columns + gap * (_columns - 1),
          height: cell * _rows + gap * (_rows - 1),
          child: Stack(
            children: [
              for (var i = 0; i < _columns * _rows; i++)
                Positioned(
                  left: (i % _columns) * (cell + gap),
                  top: (i ~/ _columns) * (cell + gap),
                  width: cell,
                  height: cell,
                  child: _SurfaceKey(index: i, data: _keys[i]),
                ),
            ],
          ),
        ),
      );
    });
  }
}

class _SurfaceKey extends StatefulWidget {
  final int index;
  final CompanionKey? data;

  const _SurfaceKey({required this.index, required this.data});

  @override
  State<_SurfaceKey> createState() => _SurfaceKeyState();
}

class _SurfaceKeyState extends State<_SurfaceKey> {
  int? _pointer;

  @override
  void dispose() {
    if (_pointer != null) _send(false);
    super.dispose();
  }

  void _send(bool pressed) {
    try {
      Injector.get<SignalRService>().companionKey(widget.index, pressed);
    } catch (_) {
      // Hub fora: o PC já soltou as teclas desta conexão quando ela caiu.
    }
  }

  void _down(PointerDownEvent e) {
    if (_pointer != null) return;
    setState(() => _pointer = e.pointer);
    ThemeController.instance.tapFeedback();
    _send(true);
  }

  void _up(int pointer) {
    if (_pointer != pointer) return;
    setState(() => _pointer = null);
    _send(false);
  }

  @override
  Widget build(BuildContext context) {
    final d = widget.data;
    final down = _pointer != null || d?.pressed == true;
    final text = d?.text ?? '';
    return Semantics(
      button: true,
      label: text.isEmpty ? tr('Tecla {n}', {'n': widget.index + 1}) : text,
      child: Listener(
        behavior: HitTestBehavior.opaque,
        onPointerDown: _down,
        onPointerUp: (e) => _up(e.pointer),
        onPointerCancel: (e) => _up(e.pointer),
        child: AnimatedScale(
          scale: down ? 0.94 : 1,
          duration: const Duration(milliseconds: 60),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: DecoratedBox(
              decoration: BoxDecoration(color: d?.color ?? const Color(0xFF17181B)),
              child: d?.image != null
                  ? Image.memory(d!.image!, fit: BoxFit.cover, gaplessPlayback: true,
                      width: double.infinity, height: double.infinity,
                      errorBuilder: (_, __, ___) => _label(text, d.textColor))
                  : _label(text, d?.textColor),
            ),
          ),
        ),
      ),
    );
  }

  Widget _label(String text, Color? color) => Center(
        child: Padding(
          padding: const EdgeInsets.all(4),
          child: Text(
            text,
            textAlign: TextAlign.center,
            maxLines: 4,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(color: color ?? Colors.white, fontSize: 12, fontWeight: FontWeight.w600),
          ),
        ),
      );
}
