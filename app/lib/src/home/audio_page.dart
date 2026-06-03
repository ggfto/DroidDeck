import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'home_controller.dart';
import 'mixer_page.dart';

/// Tela única de áudio: saídas e entradas juntas.
/// Deitado (largura > 600) = lado a lado; em pé = um scroll contínuo
/// (seção Saídas e logo abaixo Entradas), aproveitando o espaço.
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

        return LayoutBuilder(
          builder: (context, c) {
            final wide = c.maxWidth > 600; // deitado/tablet
            if (wide) {
              // Lado a lado, cada coluna com seu próprio scroll/refresh.
              return Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(
                    child: _labeled(
                      'Saídas',
                      Icons.volume_up,
                      MixerList(
                        devices: outputs,
                        controller: controller,
                        isInput: false,
                        onRefresh: controller.fetchOutputs,
                      ),
                    ),
                  ),
                  const VerticalDivider(width: 1),
                  Expanded(
                    child: _labeled(
                      'Entradas',
                      Icons.mic,
                      MixerList(
                        devices: inputs,
                        controller: controller,
                        isInput: true,
                        onRefresh: controller.fetchInputs,
                      ),
                    ),
                  ),
                ],
              );
            }

            // Em pé: scroll contínuo (Saídas, depois Entradas).
            return RefreshIndicator(
              onRefresh: () async {
                await controller.fetchOutputs();
                await controller.fetchInputs();
              },
              child: ListView(
                padding: const EdgeInsets.only(bottom: 80),
                children: [
                  _header('Saídas', Icons.volume_up),
                  if (outputs.isEmpty)
                    _empty('Nenhuma saída encontrada.')
                  else
                    ...outputs.map((e) => MixerDeviceCard(
                          item: e,
                          controller: controller,
                          isInput: false,
                        )),
                  const SizedBox(height: 8),
                  _header('Entradas', Icons.mic),
                  if (inputs.isEmpty)
                    _empty('Nenhuma entrada encontrada.')
                  else
                    ...inputs.map((e) => MixerDeviceCard(
                          item: e,
                          controller: controller,
                          isInput: true,
                        )),
                ],
              ),
            );
          },
        );
      }),
    );
  }

  // Coluna com cabeçalho + lista que preenche (modo deitado).
  Widget _labeled(String title, IconData icon, Widget list) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _header(title, icon),
        Expanded(child: list),
      ],
    );
  }

  Widget _header(String title, IconData icon) {
    return Padding(
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
    );
  }

  Widget _empty(String msg) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Center(
        child: Text(msg, style: const TextStyle(color: Colors.grey)),
      ),
    );
  }
}
