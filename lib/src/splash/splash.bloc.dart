import 'package:shared_preferences/shared_preferences.dart';

class SplashBloc {
  Future<bool> carregarConfiguracoes() async {
    SharedPreferences sharedPreferences = await SharedPreferences.getInstance();
    return sharedPreferences.getString('config') != null;
  }
}
