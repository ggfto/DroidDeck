import 'package:flutter/material.dart';

class ExpandingPanel extends StatefulWidget {
  final Widget closedContent;
  final Widget openedContent;

  const ExpandingPanel(
      {required this.closedContent, required this.openedContent});

  @override
  _ExpandingPanelState createState() => _ExpandingPanelState();
}

class _ExpandingPanelState extends State<ExpandingPanel> {
  bool _isExpanded = false;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Container(
          padding: const EdgeInsets.all(8.0),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              widget.closedContent,
              IconButton(
                icon: Icon(
                  _isExpanded
                      ? Icons.keyboard_arrow_up
                      : Icons.keyboard_arrow_down,
                ),
                onPressed: () {
                  setState(() {
                    _isExpanded = !_isExpanded;
                  });
                },
              ),
            ],
          ),
        ),
        if (_isExpanded)
          Container(
            padding: const EdgeInsets.all(16.0),
            child: widget.openedContent,
          ),
      ],
    );
  }
}
