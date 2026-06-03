import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/foundation.dart';

import 'package:network_info_plus/network_info_plus.dart';

class ServerDiscoveryService {
  Future<String> findServers(String ip, int timeout) async {
    debugPrint('Discovery: Starting search.');
    var serverPort = 7573;
    var socket = await RawDatagramSocket.bind(InternetAddress.anyIPv4, 0);
    socket.broadcastEnabled = true;

    final info = NetworkInfo();
    String? wifiIP = await info.getWifiIP();
    debugPrint('Discovery: IP=$wifiIP');

    List<String> targets = ['255.255.255.255'];

    if (wifiIP != null) {
      try {
        // Assume /24 subnet (Standard for Home WiFi)
        final lastDot = wifiIP.lastIndexOf('.');
        if (lastDot != -1) {
          String broadcast = '${wifiIP.substring(0, lastDot)}.255';
          debugPrint('Discovery: Calculated broadcast: $broadcast');
          if (!targets.contains(broadcast)) {
            targets.add(broadcast);
          }
        }
      } catch (e) {
        debugPrint('Discovery: Error calculating broadcast: $e');
      }
    }

    // fallback to generic 192.168.x.255 if nothing found?
    // Usually 255.255.255.255 covers it if routed, but calculated is better.

    for (var target in targets) {
      try {
        debugPrint('Discovery: Sending packet to $target:$serverPort');
        socket.send(utf8.encode('AnyDeckDiscoveryRequest'),
            InternetAddress(target), serverPort);
      } catch (e) {
        debugPrint('Discovery: Error sending packet to $target: $e');
      }
    }

    Completer<String> responseCompleter = Completer<String>();

    socket.listen((RawSocketEvent e) {
      if (e == RawSocketEvent.read) {
        Datagram? datagram = socket.receive();
        if (datagram != null) {
          try {
            String response = utf8.decode(datagram.data);
            debugPrint(
                'Discovery: Received response: $response from ${datagram.address}');
            final json = jsonDecode(response) as Map<String, dynamic>;
            if (json.containsKey('ip') && !responseCompleter.isCompleted) {
              responseCompleter.complete(json['ip'] as String);
            }
          } catch (e) {
            debugPrint('Discovery: Error parsing response: $e');
          }
          socket.close();
        }
      }
    });

    return responseCompleter.future.timeout(Duration(seconds: timeout),
        onTimeout: () {
      debugPrint('Discovery: Timeout waiting for response');
      socket.close();
      return 'Not Found';
    });
  }
}
