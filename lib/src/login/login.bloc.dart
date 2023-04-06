import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../home/home_page.dart';

class LoginBloc {
  var formKey = GlobalKey<FormState>();
  var emailController = TextEditingController();
  var passwordController = TextEditingController();

  Future<bool> login() async {
    SharedPreferences sharedPreferences = await SharedPreferences.getInstance();
    // var url = Uri.parse("https://restful-booker.herokuapp.com/auth");
    // var response = await http.post(url, body: {
    //   'username': _emailController.text,
    //   'password': _passwordController.text
    // });
    // return response.statusCode == 200;
    sharedPreferences.setString('token', 'token');
    return emailController.text == 'ggfto@outlook.com' &&
        passwordController.text == 'sysadmin';
  }

  Future<bool> validar(BuildContext context) async {
    FocusScopeNode currentFocus = FocusScope.of(context);
    if (formKey.currentState!.validate()) {
      if (!currentFocus.hasPrimaryFocus) {
        currentFocus.unfocus();
      }

      var loginStatus = await login();
      if (loginStatus) {
        Navigator.pushReplacement(
            context, MaterialPageRoute(builder: (context) => const HomePage()));
      } else {
        passwordController.clear();
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text(
            'Email e/ou senha inválidos',
            textAlign: TextAlign.center,
          ),
          backgroundColor: Colors.redAccent,
        ));
      }
    }
    return false;
  }
}
