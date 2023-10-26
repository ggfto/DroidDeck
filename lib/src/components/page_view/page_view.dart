import 'package:companion/src/components/page_view/page.bloc.dart';
import 'package:flutter/material.dart';

class MyPageView extends StatelessWidget {
  const MyPageView({super.key});

  @override
  Widget build(BuildContext context) {
    final bloc = PageBloc();
    final l = <Widget>[];
    l.add(
      ElevatedButton(
        onPressed: () {},
        child: const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [Icon(Icons.play_arrow)],
          ),
        ),
      ),
    );
    l.add(
      ElevatedButton(
        onPressed: () {},
        child: const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [Icon(Icons.stop)],
          ),
        ),
      ),
    );
    l.add(
      ElevatedButton(
        onPressed: () {},
        child: const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [Icon(Icons.skip_next)],
          ),
        ),
      ),
    );
    l.add(
      ElevatedButton(
        onPressed: () {},
        child: const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [Icon(Icons.skip_previous)],
          ),
        ),
      ),
    );
    final list = [
      {"nome": "Play/Pause", "icone": Icons.play_arrow},
      {"nome": "Stop", "icone": Icons.stop}
    ];
    return PageView(
      /// [PageView.scrollDirection] defaults to [Axis.horizontal].
      /// Use [Axis.vertical] to scroll vertically.
      controller: bloc.controller,
      onPageChanged: (val) => {bloc.printVal(val)},
      children: <Widget>[
        Center(
          child: GridView.count(
            crossAxisCount: 4,
            mainAxisSpacing: 10,
            crossAxisSpacing: 10,
            children: l,
          ),
        ),
        Center(
          child: ListView.builder(
            itemCount: list.length,
            itemBuilder: (context, index) {
              return ElevatedButton(
                onPressed: () {},
                child: Column(
                  children: [
                    Icon(list[index]["icone"] as IconData),
                    Text(list[index]["nome"].toString()),
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
