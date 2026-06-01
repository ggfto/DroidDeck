import 'package:flutter/material.dart';

class PassInput extends StatelessWidget {
  final TextEditingController controller;

  const PassInput({super.key, required this.controller});

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      decoration: const InputDecoration(
          labelText: "Senha", border: OutlineInputBorder()),
      controller: controller,
      obscureText: true,
      validator: (password) {
        if (password == null || password.isEmpty) {
          return 'Digite uma senha.';
        }
        if (password.length < 6) {
          return 'A senha deve conter pelo menos 7 caracteres.';
        }
        return null;
      },
    );
  }
}
