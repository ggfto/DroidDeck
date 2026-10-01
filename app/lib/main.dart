import 'package:companion/src/binding/companion_application_binding.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';

import 'src/pages/splash/splash_page.dart';
import 'src/home/home_page.dart';
import 'src/config/config_page.dart';
import 'src/welcome/welcome_page.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return CompanionCoreConfig(
      title: 'DroidDeck',
      bindings: CompanionApplicationBinding(),
      pageBuilders: [
        FlutterGetItPageBuilder(
          page: (_) => const SplashPage(),
          path: '/',
        ),
        FlutterGetItPageBuilder(
          page: (_) => const HomePage(),
          path: '/home',
        ),
        FlutterGetItPageBuilder(
          page: (_) => const ConfigPage(),
          path: '/config',
        ),
        FlutterGetItPageBuilder(
          page: (_) => const WelcomePage(),
          path: '/welcome',
        ),
      ],
    );
  }
}
