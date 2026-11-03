import 'package:flutter/material.dart';

import 'account_buttons.dart';
import 'button_look.dart';
import 'default_button.dart';
import 'discord_button.dart';
import 'key_button.dart';
import 'obs_button.dart';
import 'tuya_button.dart';
import 'windows_button.dart';

/// Desenhista de cada tipo de ação. Tipo fora daqui usa o [DefaultButtonRenderer].
/// O timer tem widget próprio (relógio que anda sozinho) e não passa por aqui.
const Map<String, ButtonRenderer> _renderers = {
  'key': KeyButtonRenderer(),
  'keyboard_key': KeyButtonRenderer(),
  'media': MediaButtonRenderer(),
  'mixer': MixerButtonRenderer(),
  'discord': DiscordButtonRenderer(),
  'obs': ObsButtonRenderer(),
  'tuya': TuyaButtonRenderer(),
  'windows': WindowsButtonRenderer(),
  'homeassistant': HomeAssistantButtonRenderer(),
  'spotify': SpotifyButtonRenderer(),
  'twitch': LiveButtonRenderer('ReceiveTwitchState', Icons.sensors),
  'youtube': LiveButtonRenderer('ReceiveYouTubeState', Icons.smart_display),
};

ButtonRenderer rendererFor(String? type) =>
    _renderers[type] ?? const DefaultButtonRenderer();
