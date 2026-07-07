import 'package:companion/core/core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'home_controller.dart';
import 'audio_page.dart';
import '../stream_deck/stream_deck_page.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  HomePageState createState() => HomePageState();
}

class HomePageState extends State<HomePage> {
  final controller = HomeController();
  final PageController _pageController = PageController();
  int selectedIndex = 0;

  @override
  void initState() {
    super.initState();
    // No web o foco é configurar o deck; não precisa do polling de mixer.
    if (!kIsWeb) controller.startPolling();
  }

  @override
  void dispose() {
    controller.dispose();
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    // No navegador (configurador no PC), abre direto no editor de deck.
    if (kIsWeb) {
      return StreamDeckPage(
        onToggleFullscreen: () {},
        isFullscreen: false,
      );
    }
    return Scaffold(
      body: Watch((context) {
        if (controller.isLoading.value && controller.outputs.value.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (controller.error.value != null) {
          final err = controller.error.value ?? '';
          // 401/403 = pareamento invalido: retry so falha de novo. Oferece voltar
          // ao pareamento pra nao prender o usuario nesta tela.
          final isAuthError = err.contains('401') || err.contains('403');
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(
                    isAuthError
                        ? 'Falha de autenticação (chave inválida ou expirada).\nRefaça o pareamento.'
                        : 'Erro: $err',
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                  if (!isAuthError)
                    ElevatedButton(
                        onPressed: controller.fetchOutputs,
                        child: const Text('Tentar novamente')),
                  TextButton.icon(
                    onPressed: () =>
                        Navigator.of(context).pushReplacementNamed('/config'),
                    icon: const Icon(Icons.qr_code_scanner),
                    label: const Text('Refazer pareamento'),
                  ),
                ],
              ),
            ),
          );
        }

        // Toggle System UI based on fullscreen state
        if (controller.isFullscreen.value) {
          SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
        } else {
          SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
        }

        return PageView(
          controller: _pageController,
          onPageChanged: (index) {
            setState(() {
              selectedIndex = index;
            });
          },
          children: [
            AudioPage(
              controller: controller,
              isFullscreen: controller.isFullscreen.value,
              onToggleFullscreen: controller.toggleFullscreen,
            ),
            StreamDeckPage(
              onToggleFullscreen: controller.toggleFullscreen,
              isFullscreen: controller.isFullscreen.value,
            ),
          ],
        );
      }),
      bottomNavigationBar: Watch((context) {
        if (controller.isFullscreen.value) {
          return const SizedBox.shrink();
        }
        return BottomNavigationBar(
          items: const <BottomNavigationBarItem>[
            BottomNavigationBarItem(
              icon: Icon(Icons.tune),
              label: 'Áudio',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.grid_view),
              label: 'Deck',
            ),
          ],
          currentIndex: selectedIndex,
          selectedItemColor: Colors.blueAccent,
          onTap: (val) {
            _pageController.animateToPage(
              val,
              duration: const Duration(milliseconds: 300),
              curve: Curves.easeInOut,
            );
            setState(() {
              selectedIndex = val;
            });
          },
        );
      }),
    );
  }
}
