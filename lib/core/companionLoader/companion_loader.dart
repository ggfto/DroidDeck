import 'package:asyncstate/asyncstate.dart';
import 'package:flutter/material.dart';
import 'package:loading_animation_widget/loading_animation_widget.dart';

final class CompanionLoader extends AsyncOverlay {
  CompanionLoader()
      : super(
            id: 0,
            builder:
                (BuildContext context, AsyncValue<RouteSettings> settings) {
              return Center(
                  child: SizedBox(
                      width: MediaQuery.sizeOf(context).width * .8,
                      child: LoadingAnimationWidget.staggeredDotsWave(
                        color: Colors.blue,
                        size: 50,
                      )));
            });
}
