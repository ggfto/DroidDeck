import 'package:companion/src/core/constants.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class SplashPage extends StatefulWidget {
  const SplashPage({super.key});

  @override
  SplashPageState createState() => SplashPageState();
}

class SplashPageState extends State<SplashPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _checkConfig());
  }

  Future<void> _checkConfig() async {
    if (kIsWeb) {
      // O web é servido pelo próprio PC: pega a chave via loopback (sem QR).
      final client = Injector.get<DroidDeckClient>();
      final signalR = Injector.get<SignalRService>();
      final origin = Uri.base.origin;
      client.setBaseUrl(origin);
      try {
        final resp = await Dio().get('$origin/api/pairing/local-key');
        final key = (resp.data is Map) ? resp.data['key'] as String? : null;
        client.setApiKey(key);
        await signalR.init(origin, apiKey: key);
      } catch (_) {
        // Aberto fora do localhost (LAN): sem chave automática.
        await signalR.init(origin);
      }
      if (mounted) Navigator.of(context).pushReplacementNamed('/home');
      return;
    }
    final sp = await SharedPreferences.getInstance();
    final ip = sp.getString(Constants.ipAddrKey);
    final apiKey = sp.getString(Constants.apiKeyKey);

    if (ip != null && ip.isNotEmpty && mounted) {
      final baseUrl = 'http://$ip:5000';

      final client = Injector.get<DroidDeckClient>();
      client.setBaseUrl(baseUrl);
      client.setApiKey(apiKey);

      // Initialize SignalR with the configured URL (use local variable, not client.baseUrl)
      final signalR = Injector.get<SignalRService>();
      await signalR.init(baseUrl, apiKey: apiKey);

      Navigator.of(context).pushReplacementNamed('/home');
    } else if (mounted) {
      Navigator.of(context).pushReplacementNamed('/config');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Image.asset(
              'assets/images/logo.png',
              width: 150,
              height: 150,
            ),
            Image.asset(
              'assets/images/app_name.png',
              width: 150,
            ),
            const SizedBox(height: 200),
            LoadingAnimationWidget.staggeredDotsWave(
              color: Colors.yellow,
              size: 50,
            ),
          ],
        ),
      ),
    );
  }
}
