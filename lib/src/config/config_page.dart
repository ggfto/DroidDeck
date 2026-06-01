import 'package:companion/src/core/constants.dart';
import 'package:companion/src/services/server_discovery_service.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:companion/core/core.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class ConfigPage extends StatefulWidget {
  const ConfigPage({super.key});

  @override
  ConfigPageState createState() => ConfigPageState();
}

class ConfigPageState extends State<ConfigPage> {
  final _ipController = TextEditingController();
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _loadIP();
  }

  Future<void> _loadIP() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      _ipController.text = prefs.getString(Constants.ipAddrKey) ?? '';
    });
  }

  Future<void> _findServer() async {
    setState(() => _isLoading = true);
    try {
      final discovery = ServerDiscoveryService();
      final result = await discovery.findServers('255.255.255.255', 5);
      if (result != 'Not Found') {
        _ipController.text = result;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Servidor encontrado: $result')),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Servidor não encontrado')),
        );
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Erro: $e')),
      );
    } finally {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _saveAndGoBack() async {
    var ip = _ipController.text.trim();
    if (ip.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Por favor insira um IP válido')),
      );
      return;
    }

    // Basic Sanitization
    ip = ip.replaceAll('http://', '').replaceAll('https://', '');
    if (ip.endsWith('/')) ip = ip.substring(0, ip.length - 1);

    // Check for port
    String baseUrl;
    if (ip.contains(':')) {
      // User provided port
      baseUrl = 'http://$ip';
    } else {
      // Default port
      baseUrl = 'http://$ip:5000';
    }

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(Constants.ipAddrKey, ip); // Save raw IP for UI

    // Update Singletons immediately
    final client = Injector.get<AnyDeckClient>();
    client.setBaseUrl(baseUrl);

    final signalR = Injector.get<SignalRService>();
    await signalR.updateUrl(baseUrl); // Use local baseUrl, not client.baseUrl

    if (mounted) {
      Navigator.of(context).pushReplacementNamed('/home');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Configuração'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          children: [
            TextField(
              controller: _ipController,
              decoration: const InputDecoration(
                labelText: 'IP do Servidor',
                hintText: '192.168.x.x',
              ),
            ),
            const SizedBox(height: 16),
            _isLoading
                ? const CircularProgressIndicator()
                : ElevatedButton.icon(
                    onPressed: _findServer,
                    icon: const Icon(Icons.search),
                    label: const Text('Pesquisar Servidor'),
                  ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _saveAndGoBack,
              child: const Text('Salvar'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  void dispose() {
    _ipController.dispose();
    super.dispose();
  }
}
