import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:droiddeck/src/connection/constants.dart';
import 'package:droiddeck/src/services/server_discovery_service.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/stream_deck/backup/backup_buttons.dart';
import 'package:droiddeck/src/config/integrations_page.dart';
import 'package:droiddeck/src/config/triggers_page.dart';
import 'package:droiddeck/src/config/pc_switcher.dart';
import 'package:droiddeck/src/connection/pc_store.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'qr_scan_page.dart';
import 'appearance_settings_page.dart';
import '../welcome/welcome_page.dart';
import 'package:url_launcher/url_launcher.dart';

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

  List<SavedPc> _pcs = [];

  Future<void> _loadIP() async {
    final prefs = await SharedPreferences.getInstance();
    var pcs = await PcStore.load();
    // PC migrado da versão antiga ainda sem nome: pergunta ao PC em uso.
    final current = pcs.where((p) => p.id == PcStore.currentId).firstOrNull;
    if (current != null && (current.name == 'PC' || current.name == current.ip)) {
      await fetchPcName(current);
      pcs = await PcStore.load();
    }
    if (!mounted) return;
    setState(() {
      _pcs = pcs;
      _ipController.text = prefs.getString(Constants.ipAddrKey) ?? '';
    });
  }

  /// PCs pareados: tocar troca de PC; o menu renomeia ou esquece.
  Widget _buildPcList() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(tr('PCs pareados'), style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 4),
        for (final pc in _pcs)
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: Icon(pc.id == PcStore.currentId
                ? Icons.radio_button_checked
                : Icons.radio_button_unchecked),
            title: Text(pc.name),
            subtitle: Text('${pc.ip}:${pc.port}'),
            onTap: pc.id == PcStore.currentId ? null : () => switchToPc(context, pc),
            trailing: PopupMenuButton<String>(
              onSelected: (v) => v == 'rename' ? _renamePc(pc) : _removePc(pc),
              itemBuilder: (_) => [
                PopupMenuItem(value: 'rename', child: Text(tr('Renomear'))),
                PopupMenuItem(value: 'remove', child: Text(tr('Esquecer este PC'))),
              ],
            ),
          ),
        Text(
          tr('Para acrescentar outro PC, pareie com o QR dele (ou digite o IP abaixo).'),
          style: TextStyle(
              fontSize: 12, color: Theme.of(context).colorScheme.onSurfaceVariant),
        ),
      ],
    );
  }

  Future<void> _renamePc(SavedPc pc) async {
    final ctrl = TextEditingController(text: pc.name);
    final name = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(tr('Nome do PC')),
        content: TextField(controller: ctrl, autofocus: true),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: Text(tr('Cancelar'))),
          FilledButton(
              onPressed: () => Navigator.pop(ctx, ctrl.text.trim()),
              child: Text(tr('Salvar'))),
        ],
      ),
    );
    if (name == null || name.isEmpty) return;
    await PcStore.rename(pc.id, name);
    _loadIP();
  }

  Future<void> _removePc(SavedPc pc) async {
    final wasCurrent = pc.id == PcStore.currentId;
    await PcStore.remove(pc.id);
    final next = await PcStore.current();
    if (!mounted) return;
    if (wasCurrent && next != null) {
      await switchToPc(context, next);
    } else {
      _loadIP();
    }
  }

  Future<void> _findServer() async {
    setState(() => _isLoading = true);
    try {
      final discovery = ServerDiscoveryService();
      final result = await discovery.findServers('255.255.255.255', 5);
      if (result != 'Not Found') {
        _ipController.text = result;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(tr('Servidor encontrado: {server}', {'server': result}))),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(tr('Servidor não encontrado'))),
        );
      }
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(friendlyError(e))),
      );
    } finally {
      setState(() => _isLoading = false);
    }
  }

  Future<void> _saveAndGoBack() async {
    var ip = _ipController.text.trim();
    if (ip.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(tr('Por favor insira um IP válido'))),
      );
      return;
    }

    // Basic Sanitization
    ip = ip.replaceAll('http://', '').replaceAll('https://', '');
    if (ip.endsWith('/')) ip = ip.substring(0, ip.length - 1);

    // Check for port
    String baseUrl;
    String port;
    if (ip.contains(':')) {
      // User provided port
      baseUrl = 'http://$ip';
      port = ip.substring(ip.indexOf(':') + 1);
    } else {
      // Default port
      port = Constants.defaultPort;
      baseUrl = 'http://$ip:$port';
    }

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(Constants.ipAddrKey, ip); // Save raw IP for UI
    await prefs.setString(Constants.portKey, port); // persiste a porta p/ o splash
    final apiKey = prefs.getString(Constants.apiKeyKey); // mantém a chave do pareamento
    final host = ip.contains(':') ? ip.substring(0, ip.indexOf(':')) : ip;
    final pc = await PcStore.upsert(ip: host, port: port, apiKey: apiKey);
    fetchPcName(pc);

    // Update Singletons immediately
    final client = Injector.get<DroidDeckClient>();
    client.setBaseUrl(baseUrl);
    client.setApiKey(apiKey);

    final signalR = Injector.get<SignalRService>();
    await signalR.updateUrl(baseUrl, apiKey: apiKey); // Use local baseUrl, not client.baseUrl

    if (mounted) {
      Navigator.of(context).pushReplacementNamed('/home');
    }
  }

  Future<void> _pairWithQr() async {
    final result = await Navigator.of(context).push<String>(
      MaterialPageRoute(builder: (_) => const QrScanPage()),
    );
    if (result == null || !mounted) return;

    final uri = Uri.tryParse(result);
    final ip = uri?.queryParameters['ip'];
    final port = uri?.queryParameters['port'] ?? Constants.defaultPort;
    final key = uri?.queryParameters['key'];

    if (uri == null ||
        uri.scheme != 'droiddeck' ||
        ip == null ||
        ip.isEmpty ||
        key == null ||
        key.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(tr('Esse QR não é de pareamento do DroidDeck. Use o da janela "Parear dispositivo (QR)" do PC.'))),
      );
      return;
    }

    final baseUrl = 'http://$ip:$port';
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(Constants.ipAddrKey, ip);
    await prefs.setString(Constants.portKey, port); // persiste a porta p/ o splash
    await prefs.setString(Constants.apiKeyKey, key);
    // Parear com outro PC acrescenta à lista; o mesmo IP atualiza a chave.
    final pc = await PcStore.upsert(ip: ip, port: port, apiKey: key);
    await fetchPcName(pc);

    final client = Injector.get<DroidDeckClient>();
    client.setBaseUrl(baseUrl);
    client.setApiKey(key);
    final signalR = Injector.get<SignalRService>();
    await signalR.updateUrl(baseUrl, apiKey: key);

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(tr('Pareado com sucesso!'))),
      );
      Navigator.of(context).pushReplacementNamed('/home');
    }
  }

  Future<void> _openRepo() async {
    final ok = await launchUrl(Uri.parse(Constants.repoUrl),
        mode: LaunchMode.externalApplication);
    if (!ok && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(tr('Não foi possível abrir o navegador.'))),
      );
    }
  }

  /// Deck no navegador: mostra a qual PC está ligado e permite esquecer a chave.
  Widget _buildBrowserPairing() {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: const Icon(Icons.computer),
      // Nome do PC (como no Windows), não o endereço.
      title: FutureBuilder<dynamic>(
        future: Injector.get<DroidDeckClient>().getJson('/api/system/info'),
        builder: (context, snap) {
          final name = (snap.data is Map) ? (snap.data as Map)['name'] as String? : null;
          return Text(name ?? Uri.base.host);
        },
      ),
      subtitle: Text(tr('Este navegador está pareado com o PC.')),
      trailing: TextButton(
        onPressed: () async {
          await (await SharedPreferences.getInstance()).remove(Constants.apiKeyKey);
          if (!mounted) return;
          Navigator.of(context).pushNamedAndRemoveUntil('/', (_) => false);
        },
        child: Text(tr('Esquecer')),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(tr('Configuração')),
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
            // No navegador o PC é o próprio endereço da página: sem lista de PCs nem IP.
            if (kIsWeb) _buildBrowserPairing(),
            if (!kIsWeb) ...[
            if (_pcs.isNotEmpty) ...[
              _buildPcList(),
              const Divider(height: 24),
            ],
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => const WelcomePage()),
                ),
                icon: const Icon(Icons.help_outline),
                label: Text(tr('Ainda não instalou no PC? Veja como')),
              ),
            ),
            TextField(
              controller: _ipController,
              decoration: InputDecoration(
                labelText: tr('IP do Servidor'),
                hintText: '192.168.x.x',
              ),
            ),
            const SizedBox(height: 16),
            _isLoading
                ? const CircularProgressIndicator()
                : ElevatedButton.icon(
                    onPressed: _findServer,
                    icon: const Icon(Icons.search),
                    label: Text(tr('Pesquisar Servidor')),
                  ),
            const SizedBox(height: 16),
            ElevatedButton.icon(
              onPressed: _pairWithQr,
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(tr('Parear (QR)')),
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _saveAndGoBack,
              child: Text(tr('Salvar')),
            ),
            ],
            const Divider(height: 32),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.palette_outlined),
              title: Text(tr('Aparência')),
              subtitle: Text(tr('Tema, cores, estilo dos botões, vibração e idioma')),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const AppearanceSettingsPage()),
              ),
            ),
            const BackupButtons(),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.extension_outlined),
              title: Text(tr('Integrações')),
              subtitle: Text(tr(
                  'Discord, OBS, Spotify, Twitch, Home Assistant, Hue, Voicemeeter e mais')),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const IntegrationsPage()),
              ),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.bolt),
              title: Text(tr('Automações')),
              subtitle: Text(tr('Apertar um botão sozinho num horário, quando um app abrir…')),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const TriggersPage()),
              ),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.star_outline),
              title: Text(tr('DroidDeck no GitHub')),
              subtitle: Text(tr(
                  'Código aberto. Se ele está sendo útil, uma estrela ajuda outras pessoas a encontrar.')),
              trailing: const Icon(Icons.open_in_new),
              onTap: _openRepo,
            ),
          ],
          ),
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
