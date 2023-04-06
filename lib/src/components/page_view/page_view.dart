import 'package:companion/src/components/page_view/page.bloc.dart';
import 'package:flutter/material.dart';

class MyPageView extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final _bloc = PageBloc();
    return PageView(
      /// [PageView.scrollDirection] defaults to [Axis.horizontal].
      /// Use [Axis.vertical] to scroll vertically.
      controller: _bloc.controller,
      onPageChanged: (val) => {_bloc.printVal(val)},
      children: const <Widget>[
        Center(
          child: Text('First Page'),
        ),
        Center(
          child: Text('Second Page'),
        ),
        Center(
          child: Text('Third Page'),
        ),
      ],
    );
  }
}
