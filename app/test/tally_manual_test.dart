import 'package:droiddeck/core/core.dart';
import 'package:droiddeck/src/services/signalr_service.dart';
import 'package:droiddeck/src/studio/tally_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:get_it/get_it.dart';
import 'package:mocktail/mocktail.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _Client extends Mock implements DroidDeckClient {}

Map<String, dynamic> _state(String manual) => {
      'manual': manual == 'program',
      'targets': {
        tallyManualTarget: {'label': 'Sinal manual (ON AIR)', 'state': manual},
        'obs/stream': {'label': 'OBS: Transmissão', 'state': 'idle'},
      },
    };

void main() {
  late SignalRService sr;
  late _Client client;

  setUpAll(() => registerFallbackValue(DeckAction(type: 'none')));

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    sr = SignalRService();
    client = _Client();
    GetIt.I.registerSingleton<SignalRService>(sr);
    GetIt.I.registerSingleton<DroidDeckClient>(client);
    when(() => client.getActionState(any())).thenAnswer((_) async => null);
    when(() => client.executeAction(any())).thenAnswer((_) async {});
    sr.connectionStatus.value = 'Connected';
  });

  tearDown(GetIt.I.reset);

  Future<void> open(WidgetTester tester, String target) async {
    await tester.pumpWidget(MaterialApp(home: TallyPage(target: target)));
    await tester.pump();
    await tester.pump();
  }

  /// Fecha a tela e deixa o temporizador dos controles acabar.
  Future<void> close(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 6));
  }

  DeckAction sent() => verify(() => client.executeAction(captureAny())).captured.single as DeckAction;

  testWidgets('desligado: o botão liga o sinal', (tester) async {
    sr.seedState(tallyEvent, _state('idle'));
    await open(tester, tallyManualTarget);

    await tester.tap(find.text('Ligar o ON AIR'));
    await tester.pump();

    expect(sent().parameters['operation'], 'manual_on');
    await close(tester);
  });

  testWidgets('ligado: mostra NO AR e o botão desliga', (tester) async {
    sr.seedState(tallyEvent, _state('program'));
    await open(tester, tallyManualTarget);

    expect(find.text('NO AR'), findsOneWidget);
    await tester.tap(find.text('Desligar o ON AIR'));
    await tester.pump();

    expect(sent().parameters['operation'], 'manual_off');
    await close(tester);
  });

  testWidgets('os controles somem sozinhos e um toque na tela os traz de volta', (tester) async {
    sr.seedState(tallyEvent, _state('idle'));
    await open(tester, tallyManualTarget);

    await tester.pump(const Duration(seconds: 6));
    expect(find.text('Ligar o ON AIR'), findsNothing);
    expect(find.text('Toque na tela para ligar ou desligar.'), findsOneWidget);

    // O toque que traz os controles não liga nada: um toque sem querer não põe no ar.
    await tester.tapAt(const Offset(400, 300));
    await tester.pump();
    expect(find.text('Ligar o ON AIR'), findsOneWidget);
    verifyNever(() => client.executeAction(any()));
    await close(tester);
  });

  testWidgets('outro alvo não oferece ligar nem desligar', (tester) async {
    sr.seedState(tallyEvent, _state('idle'));
    await open(tester, 'obs/stream');

    await tester.tapAt(const Offset(400, 300));
    await tester.pump();
    expect(find.text('Ligar o ON AIR'), findsNothing);
    expect(find.text('Toque na tela para ligar ou desligar.'), findsNothing);
    await close(tester);
  });

  testWidgets('sem conexão não oferece: o estado é desconhecido', (tester) async {
    sr.seedState(tallyEvent, _state('idle'));
    sr.connectionStatus.value = 'Reconnecting';
    await open(tester, tallyManualTarget);

    expect(find.text('SEM SINAL'), findsOneWidget);
    expect(find.text('Ligar o ON AIR'), findsNothing);
    await close(tester);
  });

  testWidgets('falha ao falar com o PC vira aviso', (tester) async {
    sr.seedState(tallyEvent, _state('idle'));
    when(() => client.executeAction(any())).thenThrow(Exception('rede'));
    await open(tester, tallyManualTarget);

    await tester.tap(find.text('Ligar o ON AIR'));
    await tester.pump();
    await tester.pump();

    expect(find.text('Não foi possível falar com o PC.'), findsOneWidget);
    await close(tester);
  });
}
