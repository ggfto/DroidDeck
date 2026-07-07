import 'package:companion/core/core.dart';
import 'package:companion/src/services/signalr_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// Configuração do plugin de Discord: o usuário cria o PRÓPRIO app no Discord
/// Developer Portal (cada um usa o seu — o RPC só funciona pro dono do app, sem
/// precisar de aprovação) e cola aqui o Client ID + Secret.
class DiscordSettingsPage extends StatefulWidget {
  const DiscordSettingsPage({super.key});

  @override
  State<DiscordSettingsPage> createState() => _DiscordSettingsPageState();
}

class _DiscordSettingsPageState extends State<DiscordSettingsPage> {
  static const redirectUri = 'http://localhost:4787/discord';

  final _idCtrl = TextEditingController();
  final _secretCtrl = TextEditingController();
  bool _busy = false;
  bool _obscure = true;

  @override
  void initState() {
    super.initState();
    // Puxa o estado atual uma vez (o signal também reflete updates ao vivo).
    Injector.get<DroidDeckClient>().getDiscordState().then((s) {
      if (s != null && mounted) {
        Injector.get<SignalRService>().discordState.value =
            Map<String, dynamic>.from(s);
      }
    }).catchError((_) {});
  }

  @override
  void dispose() {
    _idCtrl.dispose();
    _secretCtrl.dispose();
    super.dispose();
  }

  void _snack(String msg) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(msg)));
  }

  Future<void> _save() async {
    final id = _idCtrl.text.trim();
    final secret = _secretCtrl.text.trim();
    if (id.isEmpty || secret.isEmpty) {
      _snack('Preencha o Client ID e o Client Secret.');
      return;
    }
    setState(() => _busy = true);
    try {
      await Injector.get<DroidDeckClient>().setDiscordConfig(id, secret);
      // Não limpa o campo: antes o secret sumia ao salvar e parecia que não
      // tinha salvo. Ele fica visível (mascarado) confirmando que foi enviado.
      _snack('Credenciais salvas. Agora toque em "Conectar".');
    } catch (_) {
      _snack('Não foi possível salvar (servidor acessível?).');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _connect() async {
    setState(() => _busy = true);
    try {
      final state = await Injector.get<DroidDeckClient>().connectDiscord();
      Injector.get<SignalRService>().discordState.value =
          Map<String, dynamic>.from(state);
      _snack('Discord conectado!');
    } catch (e) {
      _snack(e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Discord')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _statusCard(),
          const SizedBox(height: 16),
          _guideCard(),
          const SizedBox(height: 16),
          TextField(
            controller: _idCtrl,
            decoration: const InputDecoration(
              labelText: 'Client ID',
              border: OutlineInputBorder(),
            ),
            keyboardType: TextInputType.number,
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _secretCtrl,
            obscureText: _obscure,
            decoration: InputDecoration(
              labelText: 'Client Secret',
              border: const OutlineInputBorder(),
              suffixIcon: IconButton(
                icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                onPressed: () => setState(() => _obscure = !_obscure),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _busy ? null : _save,
                  icon: const Icon(Icons.save),
                  label: const Text('Salvar credenciais'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: ElevatedButton.icon(
                  onPressed: _busy ? null : _connect,
                  icon: _busy
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.link),
                  label: const Text('Conectar'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'Ao conectar pela 1ª vez, aprove a janela que abrir no Discord do PC.',
            style: TextStyle(fontSize: 12, color: Colors.grey),
          ),
        ],
      ))),
    );
  }

  Widget _statusCard() {
    return Watch((context) {
      final ds = Injector.get<SignalRService>().discordState.value;
      final configured = ds['configured'] == true;
      final connected = ds['connected'] == true;
      final channel = ds['channelName'];

      Color color;
      IconData icon;
      String text;
      if (!configured) {
        color = Colors.orange;
        icon = Icons.warning_amber_rounded;
        text = 'Não configurado — preencha o Client ID e o Secret abaixo.';
      } else if (!connected) {
        color = Colors.blueGrey;
        icon = Icons.link_off;
        text = 'Configurado, mas desconectado. Abra o Discord no PC e toque em "Conectar".';
      } else {
        color = Colors.green;
        icon = Icons.check_circle;
        text = (channel != null && '$channel'.isNotEmpty)
            ? 'Conectado — no canal "$channel".'
            : 'Conectado.';
      }

      return Card(
        color: color.withValues(alpha: 0.15),
        child: ListTile(
          leading: Icon(icon, color: color),
          title: Text(text),
        ),
      );
    });
  }

  Widget _guideCard() {
    Widget step(String n, String t) => Padding(
          padding: const EdgeInsets.symmetric(vertical: 3),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('$n. ', style: const TextStyle(fontWeight: FontWeight.bold)),
              Expanded(child: Text(t)),
            ],
          ),
        );

    return Card(
      child: ExpansionTile(
        leading: const Icon(Icons.help_outline),
        title: const Text('Como obter o Client ID e o Secret'),
        childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        children: [
          step('1', 'Acesse discord.com/developers/applications e clique em "New Application".'),
          step('2', 'Em OAuth2, copie o Client ID e o Client Secret (Reset Secret se preciso).'),
          step('3', 'Ainda em OAuth2 → Redirects, adicione o endereço abaixo e Salve:'),
          const SizedBox(height: 8),
          InkWell(
            onTap: () {
              Clipboard.setData(const ClipboardData(text: redirectUri));
              _snack('Redirect copiado.');
            },
            child: Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: Colors.black26,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Row(
                children: [
                  const Expanded(
                    child: SelectableText(
                      redirectUri,
                      style: TextStyle(fontFamily: 'monospace'),
                    ),
                  ),
                  const Icon(Icons.copy, size: 18),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'Cada usuário usa o próprio app — o RPC do Discord só libera o dono do app, '
            'sem precisar de aprovação. Não compartilhe o seu Secret.',
            style: TextStyle(fontSize: 12, color: Colors.grey),
          ),
        ],
      ),
    );
  }
}
