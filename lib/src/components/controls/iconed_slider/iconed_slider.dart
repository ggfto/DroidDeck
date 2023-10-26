import 'package:flutter/material.dart';

class IconedSlider extends StatefulWidget {
  final String title;
  final CrossAxisAlignment? crossAxisAlignment;

  const IconedSlider({required this.title, this.crossAxisAlignment});

  @override
  _IconedSliderState createState() => _IconedSliderState();
}

class _IconedSliderState extends State<IconedSlider> {
  double currentSliderValue = 100;
  Color muteColor = Colors.white;
  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment:
          widget.crossAxisAlignment ?? CrossAxisAlignment.center,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.all(8.0),
          child: Row(children: [
            Text(widget.title),
            const Text(" - "),
            Text(currentSliderValue.toStringAsFixed(0)),
            const Text("%")
          ]),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 0),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const CircleAvatar(child: Text('A')),
              Slider(
                value: currentSliderValue,
                label: currentSliderValue.toStringAsFixed(0),
                max: 100,
                onChanged: (double value) {
                  setState(() {
                    currentSliderValue = value;
                  });
                },
              ),
              IconButton(
                icon: Icon(
                  Icons.volume_off_sharp,
                  color: muteColor,
                ),
                onPressed: () {
                  setState(() {
                    muteColor =
                        muteColor == Colors.red ? Colors.white : Colors.red;
                  });
                },
              )
            ],
          ),
        )
      ],
    );
  }
}
