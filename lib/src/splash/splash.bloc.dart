import 'package:shared_preferences/shared_preferences.dart';

class SplashBloc {
  Future<bool> verificarToken() async {
    SharedPreferences sharedPreferences = await SharedPreferences.getInstance();
    return sharedPreferences.getString('token') != null;
  }
}
