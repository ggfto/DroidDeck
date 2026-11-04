import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';

import 'button_look.dart';

Map<String, dynamic> _stateOf(String event) => Injector.get<SignalRService>().stateOf(event);

/// Tally: "abrir" e "só mostrar" pintam o botão com o estado do alvo; os demais, o sinal manual.
class TallyButtonRenderer extends ButtonRenderer {
  const TallyButtonRenderer();

  @override
  Widget build(ButtonLook look) {
    final state = _stateOf(tallyEvent);
    final op = look.op('open');
    if (op.startsWith('manual')) {
      final on = state['manual'] == true;
      return look.tile(
        active: on,
        activeColor: tallyProgramColor,
        icon: look.hasOwnIcon ? look.icon() : const Icon(Icons.sensors, size: 30),
        label: look.ownLabel ?? (on ? tr('NO AR') : 'ON AIR'),
      );
    }
    final target = look.params['target'];
    final linked = Injector.get<SignalRService>().connectionStatus.value == 'Connected';
    final mode = tallyModeOf(state, target, linked: linked);
    final targets = state['targets'];
    final entry = targets is Map ? targets[target] : null;
    final name = entry is Map ? '${entry['label'] ?? ''}' : '';
    return look.tile(
      active: mode == TallyMode.program || mode == TallyMode.preview,
      activeColor: mode == TallyMode.preview ? tallyPreviewColor : tallyProgramColor,
      icon: look.hasOwnIcon
          ? look.icon()
          : Icon(mode == TallyMode.noSignal ? Icons.sensors_off : Icons.sensors, size: 30),
      label: look.ownLabel ?? (name.isNotEmpty ? name : tr('Tally')),
    );
  }
}

/// vMix: programa e preview nas cores de transmissão; gravação, transmissão e overlays acendem.
class VmixButtonRenderer extends ButtonRenderer {
  const VmixButtonRenderer();

  /// A entrada do botão: chave (da lista), número ou nome digitados.
  Map? _input(Map<String, dynamic> state, String? value) {
    if (value == null || value.isEmpty) return null;
    for (final i in state['inputs'] as List? ?? const []) {
      if (i is Map && (i['key'] == value || '${i['number']}' == value || i['title'] == value)) return i;
    }
    return null;
  }

  @override
  Widget build(ButtonLook look) {
    final st = _stateOf('ReceiveVmixState');
    final input = _input(st, look.params['input']);
    final key = input?['key'];
    final name = input != null ? '${input['title']}' : null;
    final overlays = st['overlays'] as List? ?? const [];

    final (IconData icon, bool active, Color? color, String label) = switch (look.op('cut')) {
      'cut' => (Icons.content_cut, false, null, tr('Cortar')),
      'fade' => (Icons.gradient, false, null, 'Fade'),
      'preview' => (Icons.preview, key != null && st['preview'] == key, tallyPreviewColor, name ?? tr('Preview')),
      'program' => (Icons.videocam, key != null && st['active'] == key, tallyProgramColor, name ?? tr('Programa')),
      'quickplay' => (Icons.play_arrow, key != null && st['active'] == key, tallyProgramColor, name ?? 'Quick Play'),
      'overlay' => (Icons.layers, key != null && overlays.contains(key), tallyProgramColor, name ?? 'Overlay'),
      'overlay_off' => (Icons.layers_clear, false, null, tr('Tirar overlays')),
      'toggle_audio' => (
          input?['muted'] == true ? Icons.volume_off : Icons.volume_up,
          input?['muted'] == true,
          null,
          name ?? tr('Áudio'),
        ),
      'set_volume' => (Icons.tune, false, null, name ?? tr('Volume')),
      'master_audio' => (Icons.volume_up, false, null, tr('Áudio master')),
      'master_volume' => (Icons.tune, false, null, tr('Volume master')),
      'toggle_record' => (
          Icons.fiber_manual_record,
          st['recording'] == true,
          tallyProgramColor,
          st['recording'] == true ? tr('Gravando') : tr('Gravar'),
        ),
      'toggle_stream' => (
          Icons.sensors,
          st['streaming'] == true,
          tallyProgramColor,
          st['streaming'] == true ? tr('No ar') : tr('Transmitir'),
        ),
      'toggle_external' => (Icons.cast, st['external'] == true, null, 'External'),
      'toggle_multicorder' => (Icons.save, st['multiCorder'] == true, null, 'MultiCorder'),
      'fade_to_black' => (Icons.brightness_2, st['fadeToBlack'] == true, null, 'FTB'),
      'snapshot' => (Icons.photo_camera, false, null, tr('Captura')),
      _ => (Icons.live_tv, false, null, 'vMix'),
    };
    return look.tile(
      active: active,
      activeColor: color,
      icon: look.hasOwnIcon ? look.icon() : Icon(icon, size: 30),
      label: look.ownLabel ?? label,
    );
  }
}

/// mAirList: "no ar" acende em vermelho; os demais comandos não têm estado para ler.
class MairlistButtonRenderer extends ButtonRenderer {
  const MairlistButtonRenderer();

  @override
  Widget build(ButtonLook look) {
    final onAir = _stateOf('ReceiveMairlistState')['onAir'] == true;
    final (IconData icon, bool active, String label) = switch (look.op('on_air_toggle')) {
      'on_air_toggle' || 'on_air' || 'off_air' => (Icons.sensors, onAir, onAir ? tr('No ar') : tr('Fora do ar')),
      'automation_toggle' || 'automation_on' || 'automation_off' => (Icons.smart_toy_outlined, false, tr('Automação')),
      'next' => (Icons.skip_next, false, tr('Próximo')),
      'player_toggle' || 'player_start' => (Icons.play_arrow, false, 'Player'),
      'player_stop' => (Icons.stop, false, tr('Parar')),
      'player_fadeout' => (Icons.trending_down, false, 'Fade out'),
      'cart' => (Icons.music_note, false, 'Cart ${look.params['cart'] ?? ''}'.trim()),
      'cart_stop_all' => (Icons.music_off, false, tr('Parar carts')),
      _ => (Icons.radio, false, 'mAirList'),
    };
    return look.tile(
      active: active,
      activeColor: tallyProgramColor,
      icon: look.hasOwnIcon ? look.icon() : Icon(icon, size: 30),
      label: look.ownLabel ?? label,
    );
  }
}

/// RadioDJ: AutoDJ, assistido e entrada refletem o estado lido; o resto é só comando.
class RadioDjButtonRenderer extends ButtonRenderer {
  const RadioDjButtonRenderer();

  @override
  Widget build(ButtonLook look) {
    final st = _stateOf('ReceiveRadioDjState');
    final (IconData icon, bool active, String label) = switch (look.op('play_track')) {
      'play_track' => (Icons.play_arrow, false, tr('Tocar')),
      'stop' => (Icons.stop, false, tr('Parar')),
      'pause' => (Icons.pause, false, tr('Pausar')),
      'resume' => (Icons.play_arrow, false, tr('Retomar')),
      'restart' => (Icons.replay, false, tr('Reiniciar')),
      'play_intro' => (Icons.first_page, false, 'Intro'),
      'autodj_toggle' => (Icons.smart_toy_outlined, st['autoDj'] == true, 'AutoDJ'),
      'assisted_toggle' => (Icons.back_hand_outlined, st['assisted'] == true, tr('Assistido')),
      'input_toggle' => (Icons.mic, st['input'] == true, tr('Entrada')),
      'events_on' || 'events_off' => (Icons.event, false, tr('Eventos')),
      'cart' => (Icons.music_note, false, 'Cart ${look.params['cart'] ?? ''}'.trim()),
      'load_playlist' => (Icons.playlist_add, false, 'Playlist'),
      'load_track' => (Icons.vertical_align_top, false, tr('Faixa')),
      _ => (Icons.radio, false, 'RadioDJ'),
    };
    return look.tile(
      active: active,
      activeColor: look.op('') == 'input_toggle' ? tallyProgramColor : null,
      icon: look.hasOwnIcon ? look.icon() : Icon(icon, size: 30),
      label: look.ownLabel ?? label,
    );
  }
}
