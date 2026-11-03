import 'dart:convert';
import 'dart:io';

import 'package:droiddeck/src/stream_deck/buttons/button_icons.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('todo ícone das categorias existe no mapa', () {
    for (final (title, names) in iconCategories) {
      for (final name in names) {
        expect(buttonIcons.containsKey(name), isTrue, reason: '$title: $name');
      }
    }
  });

  test('título de categoria tem tradução (o tr recebe variável e escapa do i18n_test)', () {
    for (final lang in ['en', 'es']) {
      final strings = jsonDecode(File('assets/i18n/$lang.json').readAsStringSync()) as Map;
      for (final (title, _) in iconCategories) {
        expect(strings.containsKey(title), isTrue, reason: '$lang: $title');
      }
    }
  });

  test('todo ícone do mapa aparece no seletor', () {
    final listed = {for (final (_, names) in iconCategories) ...names};
    expect(buttonIcons.keys.where((k) => !listed.contains(k)), isEmpty);
  });
}
