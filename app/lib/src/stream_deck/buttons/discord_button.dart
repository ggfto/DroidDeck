import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:flutter/material.dart';

import 'button_look.dart';

/// Discord: mudo, ensurdecer, canal atual e modo de voz refletem o estado real.
class DiscordButtonRenderer extends ButtonRenderer {
  const DiscordButtonRenderer();

  @override
  void init(ButtonLook look) {
    try {
      final sr = Injector.get<SignalRService>();
      final before = sr.discordState.value;
      Injector.get<DroidDeckClient>().getDiscordState().then((s) {
        // Se o hub (ou um toque) mudou o estado enquanto isso, a resposta já é velha:
        // escrevê-la desfazia o toque e o botão piscava de volta.
        if (s != null && identical(sr.discordState.value, before)) {
          sr.discordState.value = Map<String, dynamic>.from(s);
        }
      }).catchError((_) {});
    } catch (_) {}
  }

  @override
  Widget build(ButtonLook look) {
    final ds = Injector.get<SignalRService>().discordState.value;
    final params = look.params;
    final op = look.op('toggleMute');
    IconData icon;
    var active = false;
    String label;

    if (op.contains('deaf')) {
      active = ds['deaf'] == true;
      icon = active ? Icons.headset_off : Icons.headset_mic;
      label = tr('Ensurdecer');
    } else if (op == 'joinchannel') {
      final channel = params['channelId'];
      active = channel != null && channel.isNotEmpty && ds['channelId'] == channel;
      icon = active ? Icons.headset : Icons.login;
      label = (params['channelName']?.isNotEmpty ?? false) ? params['channelName']! : tr('Canal');
    } else if (op == 'disconnect' || op == 'leavechannel') {
      icon = Icons.call_end;
      label = tr('Sair');
    } else if (op.contains('inputvolume')) {
      icon = op.contains('up') ? Icons.mic : Icons.mic_none;
      label = op.contains('up') ? 'Mic +' : 'Mic −';
    } else if (op.contains('outputvolume')) {
      icon = op.contains('up') ? Icons.volume_up : Icons.volume_down;
      label = op.contains('up') ? tr('Som +') : tr('Som −');
    } else if (op.contains('voicemode') || op.contains('ptt')) {
      active = ds['voiceMode'] == 'PUSH_TO_TALK';
      icon = active ? Icons.record_voice_over : Icons.graphic_eq;
      label = active ? 'PTT' : tr('Voz');
    } else if (op.startsWith('usermute')) {
      icon = Icons.person_off;
      label = (params['userName']?.isNotEmpty ?? false) ? params['userName']! : tr('Mutar');
    } else if (op == 'uservolume') {
      icon = Icons.person;
      label = (params['userName']?.isNotEmpty ?? false) ? params['userName']! : 'Vol';
    } else {
      active = ds['mute'] == true;
      icon = active ? Icons.mic_off : Icons.mic;
      label = tr('Mutar');
    }
    return look.tile(active: active, icon: Icon(icon, size: 30), label: look.ownLabel ?? label);
  }

  @override
  void optimisticTap(ButtonLook look, SignalRService signalR) {
    // Só os liga/desliga; entrar em canal, volume etc. esperam o PC.
    final op = look.op('toggleMute');
    final cur = Map<String, dynamic>.from(signalR.discordState.value);
    // Como o Discord faz: ensurdecer também muta, e desmutar tira o surdo. Sem isso o
    // botão de mudo ficava errado até a confirmação do PC.
    if (op == 'togglemute') {
      cur['mute'] = !(cur['mute'] == true);
      if (cur['mute'] == false) cur['deaf'] = false;
    } else if (op.contains('deaf')) {
      cur['deaf'] = !(cur['deaf'] == true);
      if (cur['deaf'] == true) cur['mute'] = true;
    } else if (op.contains('voicemode') || op.contains('ptt')) {
      cur['voiceMode'] = cur['voiceMode'] == 'PUSH_TO_TALK' ? 'VOICE_ACTIVITY' : 'PUSH_TO_TALK';
    } else {
      return;
    }
    signalR.discordState.value = cur;
  }
}
