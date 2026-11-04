import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/stream_deck/widgets/deck_grid_button.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:get_it/get_it.dart';

void main() {
  late SignalRService sr;

  setUp(() {
    sr = SignalRService();
    GetIt.I.registerSingleton<SignalRService>(sr);
  });

  tearDown(GetIt.I.reset);

  Future<void> pump(WidgetTester tester, {String? activeColor}) async {
    final button = DeckButton(
      id: 'b1',
      row: 0,
      column: 0,
      activeColor: activeColor,
      action: DeckAction(type: 'obs', parameters: {'operation': 'setScene', 'scene': 'Cam 1'}),
    );
    await tester.pumpWidget(MaterialApp(
      home: SizedBox(
        width: 100,
        height: 100,
        child: DeckGridButton(button: button, onTap: () {}, onLongPress: () {}),
      ),
    ));
    await tester.pump(const Duration(seconds: 1));
  }

  DeckTile tile(WidgetTester tester) => tester.widget<DeckTile>(find.byType(DeckTile));

  testWidgets('cena no programa: vermelho', (tester) async {
    sr.obsState.value = {'currentScene': 'Cam 1', 'previewScene': 'Cam 2'};
    await pump(tester);
    expect(tile(tester).active, isTrue);
    expect(tile(tester).activeColor, tallyProgramColor);
  });

  testWidgets('cena no programa respeita a cor escolhida no botão', (tester) async {
    sr.obsState.value = {'currentScene': 'Cam 1'};
    await pump(tester, activeColor: '#123456');
    expect(tile(tester).activeColor, const Color(0xFF123456));
  });

  testWidgets('cena no preview: verde, mesmo com cor escolhida', (tester) async {
    sr.obsState.value = {'currentScene': 'Cam 2', 'previewScene': 'Cam 1'};
    await pump(tester, activeColor: '#123456');
    expect(tile(tester).active, isTrue);
    expect(tile(tester).activeColor, tallyPreviewColor);
  });

  testWidgets('programa e preview na mesma cena: vale o programa', (tester) async {
    sr.obsState.value = {'currentScene': 'Cam 1', 'previewScene': 'Cam 1'};
    await pump(tester);
    expect(tile(tester).activeColor, tallyProgramColor);
  });

  testWidgets('fora dos dois: apagado', (tester) async {
    sr.obsState.value = {'currentScene': 'Cam 2', 'previewScene': 'Cam 3'};
    await pump(tester);
    expect(tile(tester).active, isFalse);
  });
}
