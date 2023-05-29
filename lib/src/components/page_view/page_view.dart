import 'package:companion/src/components/page_view/page.bloc.dart';
import 'package:flutter/material.dart';

class MyPageView extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final _bloc = PageBloc();
    final _l = <Widget>[];
    _l.add(
      ElevatedButton(
        onPressed: () {},
        child: Center(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [Icon(Icons.play_arrow), Text("Play/Pause")],
          ),
        ),
      ),
    );
    _l.add(
      ElevatedButton(
        onPressed: () {},
        child: Center(
          child: Column(
            children: [Icon(Icons.stop), Text("Parar")],
          ),
        ),
      ),
    );
    final _list = [
      {"nome": "Play/Pause", "icone": Icons.play_arrow},
      {"nome": "Stop", "icone": Icons.stop}
    ];
    return PageView(
      /// [PageView.scrollDirection] defaults to [Axis.horizontal].
      /// Use [Axis.vertical] to scroll vertically.
      controller: _bloc.controller,
      onPageChanged: (val) => {_bloc.printVal(val)},
      children: <Widget>[
        Center(
          child: GridView.count(
            crossAxisCount: 4,
            mainAxisSpacing: 10,
            crossAxisSpacing: 10,
            children: _l,
          ),
        ),
        Center(
          child: ListView.builder(
            itemCount: _list.length,
            itemBuilder: (context, index) {
              return ElevatedButton(
                onPressed: () {},
                child: Column(
                  children: [
                    Icon(_list[index]["icone"] as IconData),
                    Text(_list[index]["nome"].toString()),
                  ],
                ),
              );
            },
          ),
        ),
        const Center(
          child: Text('Second Page'),
        ),
        const Center(
          child: Text('Third Page'),
        ),
      ],
    );
  }
}
