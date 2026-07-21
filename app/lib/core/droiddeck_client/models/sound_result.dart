/// Resultado de busca da soundboard (MyInstants), proxiado pelo backend.
class SoundResult {
  final String id;
  final String title;
  final String mp3;

  SoundResult({required this.id, required this.title, required this.mp3});

  factory SoundResult.fromJson(Map<String, dynamic> json) => SoundResult(
        id: '${json['id'] ?? ''}',
        title: '${json['title'] ?? ''}',
        mp3: '${json['mp3'] ?? ''}',
      );
}
