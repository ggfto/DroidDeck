import 'package:flutter/material.dart';

class EmailInput extends StatelessWidget {
  final TextEditingController controller;
  final String? regExp;
  final Function(String?)? validator;
  final TextInputType? keyboardType;

  const EmailInput(
      {super.key,
      this.regExp,
      required this.controller,
      this.validator,
      this.keyboardType});

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      decoration: const InputDecoration(
          labelText: "Email", border: OutlineInputBorder()),
      controller: controller,
      keyboardType: keyboardType,
      validator: (email) {
        if (email == null || email.isEmpty) {
          return 'Insira um email válido.';
        }
        // if (!RegExp(regExp).hasMatch(controller.text)) {
        //   return 'Insira um email válido.';
        // }
        return null;
      },
    );
  }
}

//r"^[a-zA-Z0-9.a-zA-Z0-9.!#$%&'*+-/=?^_`{|}~]+@[a-zA-Z0-9]+\.[a-zA-Z]+"
