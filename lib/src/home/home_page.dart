import 'package:companion/src/home/home.bloc.dart';
import 'package:flutter/material.dart';
import 'package:companion/src/splash/splash_page.dart';
import 'package:shared_preferences/shared_preferences.dart';

class HomePage extends StatefulWidget {
  const HomePage({Key? key}) : super(key: key);

  @override
  HomePageState createState() => HomePageState();
}

class HomePageState extends State<HomePage> {
  final _bloc = HomeBloc();
  @override
  Widget build(BuildContext context) {
    return Scaffold(
        appBar: AppBar(actions: <Widget>[
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sair',
            onPressed: () {
              logoutRequest(context);
            },
          ),
          IconButton(
            onPressed: () {},
            icon: const Icon(Icons.person),
            tooltip: 'Usuário',
          )
        ]),
        body: _bloc.loadPage(),
        bottomNavigationBar: BottomNavigationBar(
          items: const <BottomNavigationBarItem>[
            BottomNavigationBarItem(
              icon: Icon(Icons.home),
              label: 'Home',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.grid_view),
              label: 'Pages',
            ),
          ],
          currentIndex: _bloc.selectedIndex,
          selectedItemColor: Colors.blueAccent,
          onTap: (val) {
            setState(() {
              _bloc.selectedIndex = val;
            });
          },
        ));
  }

  Future<void> logoutRequest(BuildContext context) async {
    Widget cancelButton = TextButton(
      child: const Text("Não"),
      onPressed: () {
        Navigator.of(context).pop();
      },
    );

    Widget confirmButton = TextButton(
        child: const Text("Sim"),
        onPressed: () {
          Navigator.of(context).pop();
          logout();
        });

    AlertDialog alert = AlertDialog(
      title: Text("Confirmação"),
      content: Text("Fazer lougout?"),
      actions: [
        confirmButton,
        cancelButton,
      ],
    );
    // show the dialog
    showDialog(
      context: context,
      builder: (BuildContext context) {
        return alert;
      },
    );
  }

  Future<void> logout() async {
    SharedPreferences sharedPreferences = await SharedPreferences.getInstance();
    sharedPreferences.remove('token');
    Navigator.pushReplacement(
        context, MaterialPageRoute(builder: (context) => const SplashPage()));
  }
}
