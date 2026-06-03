import 'package:companion/core/core.dart';
import '../services/signalr_service.dart';

class CompanionApplicationBinding extends ApplicationBindings {
  @override
  List<Bind<Object>> bindings() => [
        Bind.lazySingleton<DroidDeckClient>((i) => DroidDeckClient(baseUrl: '')),
        Bind.lazySingleton<SignalRService>((i) => SignalRService()),
      ];
}
