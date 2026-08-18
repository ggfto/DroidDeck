import 'package:companion/src/stream_deck/tuya_display.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Cobre o que o botão da Tuya mostra: ligado/desligado, nível de dimmer e cor RGB.
///
/// Estes casos não dão para validar no aparelho: cada casa tem um conjunto diferente
/// de dispositivos, e um interruptor comum (que é o que temos aqui) nunca exercita
/// dimmer nem RGB. Os `status`/`functions` abaixo são cópias do formato real que a
/// API da Tuya devolveu para uma lâmpada RGB+CCT.
void main() {
  group('tuyaIsOn', () {
    test('usa o DP do próprio botão quando é booleano', () {
      expect(tuyaIsOn({'switch_led': true}, 'switch_led'), isTrue);
      expect(tuyaIsOn({'switch_led': false}, 'switch_led'), isFalse);
    });

    test('cai no switch do aparelho quando o DP do botão não é booleano', () {
      // Botão de brilho ainda precisa saber se a luz está acesa.
      final status = {'switch_led': true, 'bright_value_v2': 500};
      expect(tuyaIsOn(status, 'bright_value_v2'), isTrue);
    });

    test('não confunde outros booleanos com liga/desliga', () {
      // Só DPs que começam com "switch" contam; do contrário um "child_lock"
      // ligado faria o botão parecer aceso.
      expect(tuyaIsOn({'child_lock': true}, 'bright_value_v2'), isFalse);
    });

    test('desligado quando não há informação', () {
      expect(tuyaIsOn(const {}, 'switch_led'), isFalse);
    });
  });

  group('tuyaLevel', () {
    final functions = {
      'switch_led': {'type': 'Boolean', 'values': '{}'},
      'bright_value_v2': {
        'type': 'Integer',
        'values': '{"min":10,"max":1000,"scale":0,"step":1}',
      },
    };

    test('escala pela faixa real do aparelho, não por 0..100', () {
      // 505 em 10..1000 é ~50%. Tratar como 0..100 daria 100% (saturado).
      final level = tuyaLevel({'bright_value_v2': 505}, functions, 'bright_value_v2');
      expect(level, closeTo(0.5, 0.01));
    });

    test('mínimo da faixa é 0%, não 1%', () {
      expect(tuyaLevel({'bright_value_v2': 10}, functions, 'bright_value_v2'), 0.0);
    });

    test('máximo da faixa é 100%', () {
      expect(tuyaLevel({'bright_value_v2': 1000}, functions, 'bright_value_v2'), 1.0);
    });

    test('acha o DP de brilho mesmo quando o botão é o de liga/desliga', () {
      final level = tuyaLevel({'bright_value_v2': 505}, functions, 'switch_led');
      expect(level, closeTo(0.5, 0.01));
    });

    test('null quando o aparelho não tem nada dimerizável', () {
      final onlySwitch = {
        'switch_1': {'type': 'Boolean', 'values': '{}'},
      };
      expect(tuyaLevel({'switch_1': true}, onlySwitch, 'switch_1'), isNull);
    });

    test('valores fora da faixa são fixados em 0..1', () {
      expect(tuyaLevel({'bright_value_v2': 5000}, functions, 'bright_value_v2'), 1.0);
      expect(tuyaLevel({'bright_value_v2': -5}, functions, 'bright_value_v2'), 0.0);
    });

    test('faixa ilegível cai no padrão em vez de sumir com o indicador', () {
      final quebrado = {
        'bright_value_v2': {'type': 'Integer', 'values': 'nao-e-json'},
      };
      // Sem faixa válida assume 0..100, então 50 vira metade.
      expect(tuyaLevel({'bright_value_v2': 50}, quebrado, 'bright_value_v2'),
          closeTo(0.5, 0.01));
    });
  });

  group('tuyaColor', () {
    test('converte o HSV da Tuya (h 0..360, s/v 0..1000)', () {
      final status = {
        'work_mode': 'colour',
        'colour_data_v2': '{"h":0,"s":1000,"v":1000}',
      };
      final color = tuyaColor(status);
      expect(color, isNotNull);
      // Vermelho puro.
      expect(color!.r, closeTo(1.0, 0.01));
      expect(color.g, closeTo(0.0, 0.01));
      expect(color.b, closeTo(0.0, 0.01));
    });

    test('h=240 vira azul', () {
      final color = tuyaColor({
        'work_mode': 'colour',
        'colour_data_v2': '{"h":240,"s":1000,"v":1000}',
      });
      expect(color!.b, closeTo(1.0, 0.01));
      expect(color.r, closeTo(0.0, 0.01));
    });

    test('ignora a cor em modo branco', () {
      // A Tuya mantém a última cor usada preenchida mesmo em modo branco; sem esta
      // checagem uma lâmpada em branco apareceria colorida no botão.
      final status = {
        'work_mode': 'white',
        'colour_data_v2': '{"h":0,"s":1000,"v":1000}',
      };
      expect(tuyaColor(status), isNull);
    });

    test('aceita o nome antigo colour_data', () {
      final color = tuyaColor({
        'work_mode': 'colour',
        'colour_data': '{"h":120,"s":1000,"v":1000}',
      });
      expect(color, isNotNull);
      expect(color!.g, closeTo(1.0, 0.01));
    });

    test('null em JSON inválido, sem estourar', () {
      expect(tuyaColor({'work_mode': 'colour', 'colour_data_v2': '{{{'}), isNull);
      expect(tuyaColor({'work_mode': 'colour', 'colour_data_v2': ''}), isNull);
      expect(tuyaColor(const {}), isNull);
    });
  });

  group('tuyaIconFor', () {
    test('luz muda de ícone conforme o estado', () {
      expect(tuyaIconFor('dj', true), Icons.lightbulb);
      expect(tuyaIconFor('dj', false), Icons.lightbulb_outline);
    });

    test('categoria desconhecida cai num ícone genérico de toggle', () {
      expect(tuyaIconFor('categoria-nova', true), Icons.toggle_on);
      expect(tuyaIconFor(null, false), Icons.toggle_off);
    });
  });
}
