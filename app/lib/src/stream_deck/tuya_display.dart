import 'dart:convert';

import 'package:flutter/material.dart';

/// Lógica de leitura do estado de um dispositivo Tuya para exibição no botão.
///
/// Funções puras de propósito: dependem só do `status`/`functions` que o backend manda,
/// então dá para cobrir dimmer e RGB em teste — o que não seria possível pelo aparelho,
/// já que cada casa tem um conjunto diferente de dispositivos.

/// Ligado/desligado.
///
/// Usa o DP do próprio botão quando ele é booleano; senão procura o primeiro DP de
/// liga/desliga do aparelho — um botão de brilho também precisa saber se a luz está
/// acesa, para não pintar nível de algo apagado.
bool tuyaIsOn(Map status, String code) {
  final own = status[code];
  if (own is bool) return own;

  for (final entry in status.entries) {
    if (entry.value is bool && '${entry.key}'.startsWith('switch')) {
      return entry.value as bool;
    }
  }
  return false;
}

/// Nível 0..1 de um DP numérico (dimmer, temperatura de cor, velocidade de ventilador),
/// ou null quando o aparelho não tem nada dimerizável.
///
/// Escolhe o DP do próprio botão quando ele é Integer; senão procura um de brilho.
/// A faixa vem do `specifications` do aparelho — não dá para assumir 0..100: o
/// `bright_value_v2` típico vai de 10 a 1000.
double? tuyaLevel(Map status, Map functions, String code) {
  String? target;
  if ((functions[code] as Map?)?['type'] == 'Integer') {
    target = code;
  } else {
    for (final entry in functions.entries) {
      if ((entry.value as Map)['type'] == 'Integer' &&
          '${entry.key}'.startsWith('bright')) {
        target = '${entry.key}';
        break;
      }
    }
  }
  if (target == null) return null;

  final raw = status[target];
  if (raw is! num) return null;

  var min = 0.0;
  var max = 100.0;
  final values = (functions[target] as Map?)?['values'];
  if (values is String && values.isNotEmpty) {
    try {
      final parsed = jsonDecode(values) as Map;
      min = (parsed['min'] as num?)?.toDouble() ?? min;
      max = (parsed['max'] as num?)?.toDouble() ?? max;
    } catch (_) {
      // Faixa ilegível: cai no padrão 0..100 em vez de sumir com o indicador.
    }
  }
  if (max <= min) return null;

  return ((raw.toDouble() - min) / (max - min)).clamp(0.0, 1.0);
}

/// Cor real de um dispositivo RGB, ou null se ele não estiver em modo de cor.
///
/// A Tuya manda `colour_data_v2` como JSON {h,s,v}, com h em 0..360 e s/v em 0..1000.
/// Em modo branco esse campo continua preenchido com a última cor usada, por isso só
/// vale quando `work_mode == 'colour'` — senão uma lâmpada em branco apareceria colorida.
Color? tuyaColor(Map status) {
  if (status['work_mode'] != 'colour') return null;

  final raw = status['colour_data_v2'] ?? status['colour_data'];
  if (raw is! String || raw.isEmpty) return null;

  try {
    final hsv = jsonDecode(raw) as Map;
    final h = (hsv['h'] as num?)?.toDouble() ?? 0;
    final s = (hsv['s'] as num?)?.toDouble() ?? 0;
    final v = (hsv['v'] as num?)?.toDouble() ?? 0;

    return HSVColor.fromAHSV(
      1.0,
      h.clamp(0.0, 360.0),
      (s / 1000).clamp(0.0, 1.0),
      (v / 1000).clamp(0.0, 1.0),
    ).toColor();
  } catch (_) {
    return null;
  }
}

/// Ícone por categoria da Tuya. dj/dd/dc=luz, cz/pc=tomada, fs=ventilador, wk=termostato.
IconData tuyaIconFor(String? category, bool on) {
  switch (category) {
    case 'dj':
    case 'dd':
    case 'dc':
      return on ? Icons.lightbulb : Icons.lightbulb_outline;
    case 'cz':
    case 'pc':
      return on ? Icons.power : Icons.power_off;
    case 'fs':
      return Icons.mode_fan_off;
    case 'wk':
      return Icons.thermostat;
    default:
      return on ? Icons.toggle_on : Icons.toggle_off;
  }
}
