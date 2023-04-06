import 'package:companion/src/components/inputs/email_input.dart';
import 'package:companion/src/components/inputs/pass_input.dart';
import 'package:companion/src/home/home_page.dart';
import 'package:companion/src/login/login.bloc.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:http/http.dart' as http;

class LoginPage extends StatefulWidget {
  const LoginPage({Key? key}) : super(key: key);

  @override
  LoginPageState createState() => LoginPageState();
}

class LoginPageState extends State<LoginPage> {
  final _bloc = LoginBloc();
  @override
  Widget build(BuildContext context) {
    return Scaffold(
        body: Form(
            key: _bloc.formKey,
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      EmailInput(controller: _bloc.emailController),
                      const Padding(padding: EdgeInsets.only(top: 10.0)),
                      PassInput(controller: _bloc.passwordController),
                      ElevatedButton(
                          onPressed: () {
                            setState(() {
                              _bloc.validar(context);
                            });
                          },
                          child: const Text('Login')),
                    ]),
              ),
            )));
  }
}
