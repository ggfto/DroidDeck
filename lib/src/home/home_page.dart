import 'package:companion_core/companion_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'home_controller.dart';
import 'mixer_page.dart';
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
    controller.startPolling();
  }

  @override
  void dispose() {
    controller.dispose();
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Watch((context) {
        if (controller.isLoading.value && controller.outputs.value.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (controller.error.value != null) {
          return Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text('Erro: ${controller.error.value}'),
                ElevatedButton(
                    onPressed: controller.fetchOutputs,
                    child: const Text('Tentar novamente'))
              ],
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
            MixerPage(
              title: 'Outputs',
              devices: controller.outputs.value,
              onRefresh: controller.fetchOutputs,
              controller: controller,
              isInput: false,
              isFullscreen: controller.isFullscreen.value,
              onToggleFullscreen: controller.toggleFullscreen,
            ),
            MixerPage(
              title: 'Inputs',
              devices: controller.inputs.value,
              onRefresh: controller.fetchInputs,
              controller: controller,
              isInput: true,
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
              icon: Icon(Icons.volume_up),
              label: 'Outputs',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.mic),
              label: 'Inputs',
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
