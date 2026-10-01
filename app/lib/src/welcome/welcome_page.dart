import 'package:companion/src/core/constants.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

/// Primeira tela de quem ainda não pareou. O app sozinho não faz nada: precisa do
/// DroidDeck rodando no PC. Boa parte das instalações chega pelo APK solto, sem passar
/// pelo README, e caía direto numa tela pedindo "IP do Servidor".
class WelcomePage extends StatelessWidget {
  const WelcomePage({super.key});

  static const _shareText =
      'Baixe o DroidDeck para Windows: ${Constants.siteUrl}';

  void _goToPairing(BuildContext context) {
    // Aberta a partir da configuração, só volta; no primeiro uso, segue para ela.
    final nav = Navigator.of(context);
    if (nav.canPop()) {
      nav.pop();
    } else {
      nav.pushReplacementNamed('/config');
    }
  }

  Future<void> _openSite(BuildContext context) async {
    final ok = await launchUrl(Uri.parse(Constants.siteUrl),
        mode: LaunchMode.externalApplication);
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Não foi possível abrir o navegador.')),
      );
    }
  }

  Future<void> _copyLink(BuildContext context) async {
    await Clipboard.setData(const ClipboardData(text: Constants.siteUrl));
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Link copiado.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.textTheme.bodyMedium?.copyWith(
      color: theme.colorScheme.onSurface.withValues(alpha: 0.7),
    );

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 32, 24, 24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Image.asset('assets/images/logo.png', height: 88),
              const SizedBox(height: 24),
              Text(
                'Bem-vindo ao DroidDeck',
                textAlign: TextAlign.center,
                style: theme.textTheme.headlineSmall
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              Text(
                'Este celular vira um painel de botões para o seu PC. Para '
                'funcionar, ele precisa do programa DroidDeck rodando no '
                'computador (Windows 10 ou 11).',
                textAlign: TextAlign.center,
                style: muted,
              ),
              const SizedBox(height: 28),
              _Step(
                number: 1,
                title: 'No PC, baixe o DroidDeck',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('Abra este endereço no navegador do computador:',
                        style: muted),
                    const SizedBox(height: 10),
                    _SiteBox(onTap: () => _copyLink(context)),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: () => SharePlus.instance
                                .share(ShareParams(text: _shareText)),
                            icon: const Icon(Icons.share, size: 18),
                            label: const Text('Enviar link'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: () => _copyLink(context),
                            icon: const Icon(Icons.copy, size: 18),
                            label: const Text('Copiar'),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Dica: "Enviar link" manda pelo WhatsApp ou e-mail para '
                      'você abrir no PC.',
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              _Step(
                number: 2,
                title: 'Abra o DroidDeck.exe',
                child: Text(
                  'Descompacte o arquivo e abra o DroidDeck.exe. Ele não abre '
                  'janela: fica como um ícone na bandeja, perto do relógio.',
                  style: muted,
                ),
              ),
              _Step(
                number: 3,
                title: 'Pareie este celular',
                child: Text(
                  'Clique com o botão direito no ícone do DroidDeck → '
                  '"Parear dispositivo (QR)" e escaneie o código aqui no app. '
                  'Celular e PC precisam estar na mesma rede Wi-Fi.',
                  style: muted,
                ),
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: () => _goToPairing(context),
                icon: const Icon(Icons.qr_code_scanner),
                label: const Text('Já instalei no PC: parear'),
                style: FilledButton.styleFrom(
                  backgroundColor: Colors.amber,
                  foregroundColor: Colors.black,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  textStyle: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
              const SizedBox(height: 4),
              TextButton(
                onPressed: () => _openSite(context),
                child: const Text('Ver o site neste celular'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Step extends StatelessWidget {
  final int number;
  final String title;
  final Widget child;

  const _Step({required this.number, required this.title, required this.child});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 20),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 14,
            backgroundColor: Colors.amber,
            child: Text('$number',
                style: const TextStyle(
                    color: Colors.black, fontWeight: FontWeight.bold)),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.only(top: 3, bottom: 6),
                  child: Text(title,
                      style: theme.textTheme.titleMedium
                          ?.copyWith(fontWeight: FontWeight.w600)),
                ),
                child,
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// O endereço em destaque, fácil de ler e digitar no PC. Tocar copia.
class _SiteBox extends StatelessWidget {
  final VoidCallback onTap;

  const _SiteBox({required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.amber.withValues(alpha: 0.12),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: BorderSide(color: Colors.amber.withValues(alpha: 0.6)),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: const Padding(
          padding: EdgeInsets.symmetric(vertical: 14, horizontal: 12),
          child: Text(
            Constants.siteDisplay,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              letterSpacing: 0.3,
            ),
          ),
        ),
      ),
    );
  }
}
