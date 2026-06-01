import 'package:companion/core/core.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

class CompanionCoreConfig extends StatelessWidget {
  const CompanionCoreConfig({
    super.key,
    this.bindings,
    this.modules,
    this.pageBuilders,
    this.pages,
    required this.title,
  });

  final String title;
  final ApplicationBindings? bindings;
  final List<FlutterGetItPageRouter>? pages;
  final List<FlutterGetItPageBuilder>? pageBuilders;
  final List<FlutterGetItModule>? modules;

  @override
  Widget build(BuildContext context) {
    return FlutterGetIt(
      debugMode: kDebugMode,
      bindings: bindings,
      pages: [...pages ?? [], ...pageBuilders ?? []],
      modules: modules,
      builder: (context, routes, flutterGetItNavObserver) {
        return AsyncStateBuilder(
          loader: CompanionLoader(),
          builder: (navigatorObserver) {
            return MaterialApp(
              debugShowCheckedModeBanner: false,
              routes: routes,
              title: title,
              navigatorObservers: [
                navigatorObserver,
                flutterGetItNavObserver,
              ],
              theme: ThemeData(
                primarySwatch: Colors.blue,
              ),
              darkTheme: ThemeData.dark(),
              themeMode: ThemeMode.system,
            );
          },
        );
      },
    );
  }
}
