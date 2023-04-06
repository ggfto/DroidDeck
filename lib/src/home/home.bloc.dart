import 'package:companion/src/components/page_view/page_view.dart';
import 'package:flutter/material.dart';

class HomeBloc {
  int selectedIndex = 0;

  Widget loadPage() {
    if (selectedIndex == 0) {
      return Text("0");
    }
    return MyPageView();
  }
}
