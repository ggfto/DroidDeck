import 'package:flutter/material.dart';

class RegexInput extends StatefulWidget {
  final String labelText;
  final String regex;
  final String errorMessage;
  final bool isPassword;
  final TextInputType keyboardType;
  final TextStyle? inputStyle;

  const RegexInput({
    super.key,
    required this.labelText,
    required this.regex,
    required this.errorMessage,
    this.isPassword = false,
    this.keyboardType = TextInputType.text,
    this.inputStyle,
  });

  @override
  RegexInputState createState() => RegexInputState();
}

class RegexInputState extends State<RegexInput> {
  final _textController = TextEditingController();
  bool _isValid = true;

  @override
  void dispose() {
    _textController.dispose();
    super.dispose();
  }

  bool _validateInput(String input) {
    if (widget.regex.isEmpty) return true;
    final regex = RegExp(widget.regex);
    return regex.hasMatch(input);
  }

  void _validateAndUpdate(String input) {
    setState(() {
      _isValid = _validateInput(input);
    });
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: _textController,
      obscureText: widget.isPassword,
      keyboardType: widget.keyboardType,
      style: widget.inputStyle,
      decoration: InputDecoration(
        labelText: widget.labelText,
        errorText: _isValid ? null : widget.errorMessage,
        border: const OutlineInputBorder(),
      ),
      onChanged: _validateAndUpdate,
    );
  }
}
