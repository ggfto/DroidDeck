import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';

import 'account_integration_page.dart';
import 'discord_settings_page.dart';
import 'obs_settings_page.dart';
import 'soundboard_settings_page.dart';
import 'tuya_settings_page.dart';

/// Lista de todas as integrações, com o atalho para a tela de cada uma.
class IntegrationsPage extends StatelessWidget {
  const IntegrationsPage({super.key});

  /// Tela de configuração pela chave usada no schema (`settings`).
  static Widget? pageFor(String? key) => switch (key) {
        'obs' => const ObsSettingsPage(),
        'discord' => const DiscordSettingsPage(),
        'tuya' => const TuyaSettingsPage(),
        'soundboard' => const SoundboardSettingsPage(),
        'homeassistant' => const AccountIntegrationPage(spec: homeAssistantSpec),
        'spotify' => const AccountIntegrationPage(spec: spotifySpec),
        'twitch' => const AccountIntegrationPage(spec: twitchSpec),
        'youtube' => const AccountIntegrationPage(spec: youTubeSpec),
        'hue' => const AccountIntegrationPage(spec: hueSpec),
        'mqtt' => const AccountIntegrationPage(spec: mqttSpec),
        'streamerbot' => const AccountIntegrationPage(spec: streamerBotSpec),
        'vtubestudio' => const AccountIntegrationPage(spec: vtubeStudioSpec),
        'vmix' => const AccountIntegrationPage(spec: vmixSpec),
        'companion' => const AccountIntegrationPage(spec: companionSpec),
        'mairlist' => const AccountIntegrationPage(spec: mairlistSpec),
        'radiodj' => const AccountIntegrationPage(spec: radioDjSpec),
        'tally' => const TallyPage(),
        _ => null,
      };

  static const _entries = [
    ('discord', Icons.headset_mic, 'Discord', 'Mudo, canais de voz, volume por usuário'),
    ('obs', Icons.videocam, 'OBS Studio', 'Cenas, gravação, transmissão, fontes'),
    ('twitch', Icons.sensors, 'Twitch', 'Marcador, clipe, anúncio, chat e enquete'),
    ('youtube', Icons.smart_display_outlined, 'YouTube', 'Chat da live e espectadores'),
    ('spotify', Icons.music_note, 'Spotify', 'Player, curtir, playlists e capa no botão'),
    ('soundboard', Icons.library_music, 'Soundboard', 'Sons do MyInstants: saída e volume'),
    ('homeassistant', Icons.home_outlined, 'Home Assistant', 'Luzes, cenas, scripts e sensores'),
    ('tuya', Icons.lightbulb_outline, 'Casa inteligente (Tuya)', 'Smart Life, Nova Digital e outras marcas'),
    ('hue', Icons.emoji_objects_outlined, 'Philips Hue', 'Luzes, salas e cenas pelo Hue Bridge'),
    ('mqtt', Icons.hub_outlined, 'MQTT', 'Publicar mensagens no seu broker'),
    ('streamerbot', Icons.smart_toy_outlined, 'Streamer.bot', 'Ações do Streamer.bot, com argumentos'),
    ('vtubestudio', Icons.face, 'VTube Studio', 'Atalhos e modelos do VTube Studio'),
    ('tally', Icons.sensors, 'Tally e ON AIR', 'O celular em tela cheia como luz de no ar'),
    ('vmix', Icons.live_tv, 'vMix', 'Corte, entradas, overlays, gravação e tally'),
    ('companion', Icons.grid_view, 'Companion', 'O celular como superfície do Bitfocus Companion'),
    ('mairlist', Icons.radio, 'mAirList', 'No ar, automação, players e cartwall'),
    ('radiodj', Icons.radio, 'RadioDJ', 'Player, AutoDJ, entrada ao vivo e carts'),
  ];

  @override
  Widget build(BuildContext context) => const _IntegrationList();
}

/// A lista, com quem está conectado (GET /api/integrations). Recarrega ao voltar de uma tela.
class _IntegrationList extends StatefulWidget {
  const _IntegrationList();

  @override
  State<_IntegrationList> createState() => _IntegrationListState();
}

class _IntegrationListState extends State<_IntegrationList> {
  Map<String, bool> _connected = {};

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final list = await Injector.get<DroidDeckClient>().getJson('/api/integrations') as List;
      if (!mounted) return;
      setState(() => _connected = {
            for (final i in list) '${(i as Map)['name']}': i['connected'] == true,
          });
    } catch (_) {
      // PC antigo, sem a lista: fica sem os indicadores.
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: Text(tr('Integrações'))),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 560),
          child: ListView(
            children: [
              for (final (key, icon, title, subtitle) in IntegrationsPage._entries)
                ListTile(
                  leading: Icon(icon),
                  title: Text(tr(title)),
                  subtitle: Text(tr(subtitle)),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (_connected[key] == true && key != 'soundboard')
                        Text(tr('Conectado'),
                            style: TextStyle(fontSize: 12, color: Colors.green.shade400)),
                      Icon(Icons.chevron_right, color: scheme.onSurfaceVariant),
                    ],
                  ),
                  onTap: () async {
                    await Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => IntegrationsPage.pageFor(key)!));
                    _load();
                  },
                ),
            ],
          ),
        ),
      ),
    );
  }
}
