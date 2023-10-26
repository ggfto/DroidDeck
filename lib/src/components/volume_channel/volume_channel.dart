import 'package:companion/src/model/channel.dart';
import 'package:flutter/material.dart';

class VolumeChannel extends StatefulWidget {
  const VolumeChannel({super.key, required this.channel});

  final Channel channel;

  @override
  VolumeChannelState createState() => VolumeChannelState(this.channel);
}

class VolumeChannelState extends State<VolumeChannel> {
  late final Channel channel;
  VolumeChannelState(Channel channel) {
    this.channel = channel;
  }

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: const CircleAvatar(child: Text('A')),
      title: Text(channel.description),
    );
  }
}
