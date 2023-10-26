import 'package:companion/src/components/controls/expanding_panel/expanding_panel.dart';
import 'package:companion/src/components/controls/iconed_slider/iconed_slider.dart';
import 'package:companion/src/components/page_view/page_view.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../config/config_page.dart';

class HomePage extends StatefulWidget {
  const HomePage({Key? key}) : super(key: key);

  @override
  HomePageState createState() => HomePageState();
}

class HomePageState extends State<HomePage> {
  int selectedIndex = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
        appBar: AppBar(actions: <Widget>[
          IconButton(
            icon: const Icon(Icons.settings),
            tooltip: 'Configurações',
            onPressed: () {
              Navigator.pushReplacement(context,
                  MaterialPageRoute(builder: (context) => ConfigPage()));
            },
          ),
        ]),
        body: loadPage(),
        bottomNavigationBar: BottomNavigationBar(
          items: const <BottomNavigationBarItem>[
            BottomNavigationBarItem(
              icon: Icon(Icons.home),
              label: 'Home',
            ),
            BottomNavigationBarItem(
                icon: Icon(Icons.volume_up_rounded), label: 'Mídias'),
            BottomNavigationBarItem(
              icon: Icon(Icons.grid_view),
              label: 'Atalhos',
            ),
          ],
          currentIndex: selectedIndex,
          selectedItemColor: Colors.blueAccent,
          onTap: (val) {
            setState(() {
              selectedIndex = val;
            });
          },
        ));
  }

  Widget loadPage() {
    double currentSliderValue = 100;
    if (selectedIndex == 1) {
      return ListView(children: <Widget>[
        ListTile(
          leading: const CircleAvatar(child: Text('A')),
          title: const Text('Dispositivo'),
          subtitle: Slider(
            value: currentSliderValue,
            max: 100,
            label: currentSliderValue.round().toString(),
            onChanged: (double value) {
              currentSliderValue = value;
            },
          ),
          trailing: const Icon(Icons.favorite_rounded),
        ),
        ListTile(
          leading: const CircleAvatar(child: Text('B')),
          title: const Text('App 1'),
          subtitle: Slider(
            value: currentSliderValue,
            max: 100,
            label: currentSliderValue.round().toString(),
            onChanged: (double value) {
              currentSliderValue = value;
            },
          ),
          trailing: const Icon(Icons.favorite_rounded),
        ),
        ListTile(
          leading: const CircleAvatar(child: Text('C')),
          title: const Text('App 2'),
          subtitle: Slider(
            value: currentSliderValue,
            max: 100,
            label: currentSliderValue.round().toString(),
            onChanged: (double value) {
              currentSliderValue = value;
            },
          ),
          trailing: const Icon(Icons.favorite_rounded),
        ),
      ]);
    } else if (selectedIndex == 2) {
      return const MyPageView();
    } else {
      return const Column(children: [
        ExpandingPanel(
            closedContent: IconedSlider(
              title: "Headset",
              crossAxisAlignment: CrossAxisAlignment.start,
            ),
            openedContent: Column(children: <Widget>[
              IconedSlider(title: "Discord"),
              IconedSlider(title: "Opera"),
              IconedSlider(title: "Spotify")
            ])),
        ExpandingPanel(
            closedContent: IconedSlider(
              title: "Alto-Falantes",
              crossAxisAlignment: CrossAxisAlignment.start,
            ),
            openedContent: Column(children: <Widget>[
              IconedSlider(title: "Discord"),
              IconedSlider(title: "Opera"),
              IconedSlider(title: "Spotify")
            ]))
      ]);
    }
  }
}
