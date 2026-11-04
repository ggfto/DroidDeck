import 'package:droiddeck/src/studio/companion_surface_page.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('tallyModeOf', () {
    final state = <String, dynamic>{
      'targets': {
        'obs/scene:Cam 1': {'label': 'OBS: Cam 1', 'state': 'program'},
        'obs/scene:Cam 2': {'label': 'OBS: Cam 2', 'state': 'preview'},
        'obs/scene:Cam 3': {'label': 'OBS: Cam 3', 'state': 'idle'},
      },
    };

    test('segue o estado publicado pelo PC', () {
      expect(tallyModeOf(state, 'obs/scene:Cam 1', linked: true), TallyMode.program);
      expect(tallyModeOf(state, 'obs/scene:Cam 2', linked: true), TallyMode.preview);
      expect(tallyModeOf(state, 'obs/scene:Cam 3', linked: true), TallyMode.idle);
    });

    test('sem conexão nunca diz "fora do ar"', () {
      expect(tallyModeOf(state, 'obs/scene:Cam 1', linked: false), TallyMode.noSignal);
      expect(tallyModeOf(state, 'obs/scene:Cam 3', linked: false), TallyMode.noSignal);
    });

    test('alvo que sumiu (programa desconectado) fica sem sinal', () {
      expect(tallyModeOf(state, 'vmix/input:x', linked: true), TallyMode.noSignal);
      expect(tallyModeOf(const {}, 'obs/stream', linked: true), TallyMode.noSignal);
      expect(tallyModeOf(state, null, linked: true), TallyMode.noSignal);
    });

    test('estado desconhecido fica sem sinal', () {
      expect(
        tallyModeOf({
          'targets': {
            'a': {'state': 'outro'}
          }
        }, 'a', linked: true),
        TallyMode.noSignal,
      );
    });
  });

  group('CompanionKey', () {
    test('lê imagem, cores e texto', () {
      final k = CompanionKey.fromJson({
        'key': 5,
        'image': 'QUJD',
        'color': '#ff0000',
        'textColor': '#ffffff',
        'text': 'CAM 1',
        'pressed': true,
      });
      expect(k.key, 5);
      expect(k.image, [65, 66, 67]);
      expect(k.color, const Color(0xFFFF0000));
      expect(k.text, 'CAM 1');
      expect(k.pressed, isTrue);
    });

    test('imagem quebrada não derruba a tecla', () {
      final k = CompanionKey.fromJson({'key': 1, 'image': '%%%', 'text': 'A'});
      expect(k.image, isNull);
      expect(k.text, 'A');
      expect(k.pressed, isFalse);
    });
  });
}
