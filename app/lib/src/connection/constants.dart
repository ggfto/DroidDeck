class Constants {
  static const ipAddrKey = "SRV_IP_ADDR";
  static const apiKeyKey = "SRV_API_KEY";
  static const portKey = "SRV_PORT";

  /// Porta padrão do backend (usada quando o pareamento não informa outra).
  static const defaultPort = "4787";

  /// Site do projeto (download do DroidDeck para Windows). [siteDisplay] é a forma curta,
  /// para a pessoa digitar no navegador do PC. Sem `?lang=`: o site escolhe o idioma pelo
  /// navegador de quem abre.
  static const siteUrl = "https://me.gf2.in/DroidDeck/";
  static const siteDisplay = "me.gf2.in/DroidDeck";

  /// Código-fonte do projeto.
  static const repoUrl = "https://github.com/ggfto/DroidDeck";
}
