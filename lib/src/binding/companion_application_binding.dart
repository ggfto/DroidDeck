import 'package:companion_core/companion_core.dart';
import '../services/signalr_service.dart';

class CompanionApplicationBinding extends ApplicationBindings {
  @override
  List<Bind<Object>> bindings() => [
        Bind.lazySingleton<AnyDeckClient>((i) => AnyDeckClient(baseUrl: '')),
        Bind.lazySingleton<SignalRService>((i) => SignalRService()),
      ];
}
