import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'home_controller.dart';
import 'mixer_page.dart';

/// Tela única de áudio: saídas e entradas juntas.
/// Deitado (largura > 600) = lado a lado; em pé = empilhado (uma metade cada).
class AudioPage extends StatelessWidget {
  final HomeController controller;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  const AudioPage({
    super.key,
    required this.controller,
    required this.isFullscreen,
    required this.onToggleFullscreen,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton(
        onPressed: onToggleFullscreen,
        mini: true,
        backgroundColor: Colors.black45,
        child: Icon(
          isFullscreen ? Icons.fullscreen_exit : Icons.fullscreen,
          color: Colors.white,
        ),
      ),
      appBar: isFullscreen
          ? null
          : AppBar(
              title: const Text('Áudio'),
              actions: [
                IconButton(
                  icon: const Icon(Icons.settings),
                  tooltip: 'Configurações',
                  onPressed: () => Navigator.of(context).pushNamed('/config'),
                ),
              ],
            ),
      body: Watch((context) {
        final outputs = controller.outputs.value;
        final inputs = controller.inputs.value;

        final out = _section(
          'Saídas',
          Icons.volume_up,
          MixerList(
            devices: outputs,
            controller: controller,
            isInput: false,
            onRefresh: controller.fetchOutputs,
          ),
        );
        final inp = _section(
          'Entradas',
          Icons.mic,
          MixerList(
            devices: inputs,
            controller: controller,
            isInput: true,
            onRefresh: controller.fetchInputs,
          ),
        );

        return LayoutBuilder(
          builder: (context, c) {
            final wide = c.maxWidth > 600; // deitado/tablet
            if (wide) {
              return Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(child: out),
                  const VerticalDivider(width: 1),
                  Expanded(child: inp),
                ],
              );
            }
            return Column(
              children: [
                Expanded(child: out),
                const Divider(height: 1),
                Expanded(child: inp),
              ],
            );
          },
        );
      }),
    );
  }

  Widget _section(String title, IconData icon, Widget child) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 6),
          child: Row(
            children: [
              Icon(icon, size: 18, color: Colors.grey),
              const SizedBox(width: 8),
              Text(
                title.toUpperCase(),
                style: const TextStyle(
                  fontWeight: FontWeight.bold,
                  color: Colors.grey,
                  fontSize: 12,
                  letterSpacing: 0.5,
                ),
              ),
            ],
          ),
        ),
        Expanded(child: child),
      ],
    );
  }
}
