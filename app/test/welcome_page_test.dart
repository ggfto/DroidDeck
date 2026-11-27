import 'package:droiddeck/src/connection/constants.dart';
import 'package:droiddeck/src/welcome/welcome_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Widget app() => MaterialApp(
        routes: {
          '/': (_) => const WelcomePage(),
          '/config': (_) => const Scaffold(body: Text('tela de pareamento')),
        },
      );

  testWidgets('mostra o endereço do site para digitar no PC', (tester) async {
    await tester.pumpWidget(app());

    expect(find.text(Constants.siteDisplay), findsOneWidget);
    expect(find.text('Enviar link'), findsOneWidget);
  });

  testWidgets('no primeiro uso, "parear" segue para a configuração',
      (tester) async {
    await tester.pumpWidget(app());

    await tester.ensureVisible(find.text('Já instalei no PC: parear'));
    await tester.tap(find.text('Já instalei no PC: parear'));
    await tester.pumpAndSettle();

    expect(find.text('tela de pareamento'), findsOneWidget);
    expect(find.byType(WelcomePage), findsNothing);
  });

  testWidgets('aberta pela configuração, "parear" só volta', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const WelcomePage()),
            ),
            child: const Text('configuração'),
          ),
        ),
      ),
    ));

    await tester.tap(find.text('configuração'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Já instalei no PC: parear'));
    await tester.tap(find.text('Já instalei no PC: parear'));
    await tester.pumpAndSettle();

    expect(find.text('configuração'), findsOneWidget);
    expect(find.byType(WelcomePage), findsNothing);
  });

  test('o link do site usa o endereço exibido e não força idioma', () {
    expect(Constants.siteUrl, startsWith('https://${Constants.siteDisplay}/'));
    // O idioma fica por conta do navegador de quem abre o link.
    expect(Constants.siteUrl, isNot(contains('lang=')));
  });
}
