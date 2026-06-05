/// Dados extraídos do QR de conexão do OBS.
/// `host` é sempre `localhost` (o backend fala com o OBS local; o IP do QR,
/// que é o endereço LAN do PC, é ignorado de propósito).
typedef ObsQrInfo = ({String host, int port, String? password});

/// Faz o parse do QR "Mostrar Informações de Conexão" do obs-websocket.
///
/// Formatos suportados:
///  - v5 (OBS 28+): `obsws://ip:porta/senha` (e `obswss://` p/ TLS). A senha vem
///    percent-encoded (`QUrl::toPercentEncoding`), então é decodificada aqui.
///    Sem autenticação o QR vem sem a senha: `obsws://ip:porta`.
///  - v4 legado: `obswebsocket|ip:porta|senha`.
///
/// Retorna `null` se a string não for um QR de OBS reconhecível.
ObsQrInfo? parseObsQr(String raw) {
  raw = raw.trim();

  for (final scheme in const ['obsws://', 'obswss://']) {
    if (raw.toLowerCase().startsWith(scheme)) {
      final rest = raw.substring(scheme.length);
      final slash = rest.indexOf('/');
      final authority = slash >= 0 ? rest.substring(0, slash) : rest;
      final encPass = slash >= 0 ? rest.substring(slash + 1) : '';
      final port = _portOf(authority);
      if (port == null) return null;
      String? pass;
      if (encPass.isNotEmpty) {
        try {
          pass = Uri.decodeComponent(encPass);
        } catch (_) {
          pass = encPass;
        }
      }
      return (host: 'localhost', port: port, password: pass);
    }
  }

  if (raw.toLowerCase().startsWith('obswebsocket|')) {
    final parts = raw.split('|');
    if (parts.length >= 2) {
      final port = _portOf(parts[1]);
      if (port == null) return null;
      final pass = parts.length >= 3 && parts[2].isNotEmpty ? parts[2] : null;
      return (host: 'localhost', port: port, password: pass);
    }
  }

  return null;
}

/// Extrai a porta de "host:porta" (host pode ser IPv4/hostname).
int? _portOf(String authority) {
  final colon = authority.lastIndexOf(':');
  if (colon < 0) return null;
  return int.tryParse(authority.substring(colon + 1).trim());
}
