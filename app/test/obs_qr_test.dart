import 'package:companion/src/config/obs_qr.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('parseObsQr', () {
    test('v5 com senha simples', () {
      final r = parseObsQr('obsws://192.168.68.119:4455/651kX2avxDfdmbLk');
      expect(r, isNotNull);
      expect(r!.host, 'localhost'); // ignora o IP do QR de propósito
      expect(r.port, 4455);
      expect(r.password, '651kX2avxDfdmbLk');
    });

    test('v5 decodifica senha percent-encoded (+, /, =, espaço)', () {
      // QUrl::toPercentEncoding: '+' -> %2B, '/' -> %2F, '=' -> %3D, ' ' -> %20
      final r = parseObsQr('obsws://10.0.0.5:4455/ab%2Bcd%2Fef%3D%20gh');
      expect(r, isNotNull);
      expect(r!.port, 4455);
      expect(r.password, 'ab+cd/ef= gh');
    });

    test('v5 sem autenticação (sem senha)', () {
      final r = parseObsQr('obsws://192.168.1.10:4455');
      expect(r, isNotNull);
      expect(r!.port, 4455);
      expect(r.password, isNull);
    });

    test('obswss:// (TLS) também é aceito', () {
      final r = parseObsQr('obswss://192.168.1.10:4455/segredo');
      expect(r, isNotNull);
      expect(r!.port, 4455);
      expect(r.password, 'segredo');
    });

    test('esquema é case-insensitive', () {
      final r = parseObsQr('OBSWS://192.168.1.10:4455/x');
      expect(r, isNotNull);
      expect(r!.port, 4455);
      expect(r.password, 'x');
    });

    test('v4 legado obswebsocket|ip:porta|senha', () {
      final r = parseObsQr('obswebsocket|192.168.1.10:4455|minhasenha');
      expect(r, isNotNull);
      expect(r!.host, 'localhost');
      expect(r.port, 4455);
      expect(r.password, 'minhasenha');
    });

    test('v4 legado sem senha', () {
      final r = parseObsQr('obswebsocket|192.168.1.10:4455|');
      expect(r, isNotNull);
      expect(r!.port, 4455);
      expect(r.password, isNull);
    });

    test('porta inválida -> null', () {
      expect(parseObsQr('obsws://192.168.1.10:abc/x'), isNull);
    });

    test('string não-OBS (QR de pareamento) -> null', () {
      expect(parseObsQr('droiddeck://pair?ip=1.2.3.4&port=5000&key=abc'), isNull);
    });

    test('lixo -> null', () {
      expect(parseObsQr('qualquer coisa'), isNull);
      expect(parseObsQr(''), isNull);
    });

    test('espaços ao redor são tolerados', () {
      final r = parseObsQr('  obsws://192.168.1.10:4455/x  ');
      expect(r, isNotNull);
      expect(r!.port, 4455);
    });
  });
}
