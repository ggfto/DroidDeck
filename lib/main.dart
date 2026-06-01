import 'package:companion/src/binding/companion_application_binding.dart';
import 'package:companion/src/core/constants.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'package:onesignal_flutter/onesignal_flutter.dart';

import 'src/pages/splash/splash_page.dart';
import 'src/home/home_page.dart';
import 'src/config/config_page.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    OneSignal.Debug.setLogLevel(OSLogLevel.verbose);
    OneSignal.initialize(Constants.oneSignalApiKey);
    OneSignal.Notifications.requestPermission(true);

    return CompanionCoreConfig(
      title: 'Companion App',
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
      ],
    );
  }
}
