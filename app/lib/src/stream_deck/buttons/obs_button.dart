import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';

import 'button_look.dart';

/// OBS: cena atual, gravando, ao vivo, câmera virtual, replay e pausa refletem o OBS.
class ObsButtonRenderer extends ButtonRenderer {
  const ObsButtonRenderer();

  @override
  Widget build(ButtonLook look) {
    final ds = Injector.get<SignalRService>().obsState.value;
    final params = look.params;
    String named(String key, String fallback) =>
        (params[key]?.isNotEmpty ?? false) ? params[key]! : fallback;

    final op = look.op('setScene');
    final scene = params['scene'] ?? '';
    final inProgram = scene.isNotEmpty && ds['currentScene'] == scene;
    final inPreview = scene.isNotEmpty && !inProgram && ds['previewScene'] == scene;
    // Cena: vermelho no programa (ou a cor escolhida no botão), verde no preview.
    final Color? stateColor = op != 'setscene'
        ? null
        : inPreview
            ? tallyPreviewColor
            : look.activeColor ?? tallyProgramColor;

    final (IconData icon, bool active, String label) = switch (op) {
      'setscene' => (
          Icons.movie_outlined,
          inProgram || inPreview,
          named('scene', tr('Cena')),
        ),
      'togglerecord' => (
          Icons.fiber_manual_record,
          ds['recording'] == true,
          ds['recording'] == true ? tr('Gravando') : tr('Gravar'),
        ),
      'togglestream' => (
          Icons.sensors,
          ds['streaming'] == true,
          ds['streaming'] == true ? tr('No ar') : 'Live',
        ),
      'togglevirtualcam' => (Icons.videocam, ds['virtualCam'] == true, tr('Câmera virtual')),
      'togglereplaybuffer' => (Icons.history, ds['replayBuffer'] == true, 'Replay buffer'),
      'savereplay' => (Icons.save_alt, false, tr('Salvar replay')),
      'toggleinputmute' => (Icons.mic_off, false, named('inputName', tr('Mutar fonte'))),
      'togglesourcevisibility' => (Icons.visibility, false, named('sourceName', tr('Fonte'))),
      'togglefilter' => (Icons.auto_fix_high, false, named('filterName', tr('Filtro'))),
      'togglerecordpause' => (
          ds['recordPaused'] == true ? Icons.play_arrow : Icons.pause,
          ds['recordPaused'] == true,
          ds['recordPaused'] == true ? tr('Retomar') : tr('Pausar gravação'),
        ),
      'createchapter' => (Icons.bookmark_add, false, tr('Capítulo')),
      'screenshot' => (Icons.photo_camera, false, tr('Captura')),
      _ => (Icons.videocam, false, 'OBS'),
    };
    return look.tile(
      active: active,
      activeColor: stateColor,
      icon: Icon(icon, size: 30),
      label: look.ownLabel ?? label,
    );
  }
}
