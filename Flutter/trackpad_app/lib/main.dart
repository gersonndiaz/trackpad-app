import 'dart:convert'; // JSON encode/decode
import 'dart:io'; // RawDatagramSocket, Socket
import 'package:flutter/material.dart'; // Flutter Widgets

void main() => runApp(const MyApp());

/// Modelo para almacenar cada host descubierto
class ServerInfo {
  final String name; // Nombre del equipo
  final String ip; // Dirección IPv4
  final int port; // Puerto TCP donde escucha el servidor
  DateTime lastSeen; // Marca de tiempo de la última vez que lo vimos

  ServerInfo(this.name, this.ip, this.port) : lastSeen = DateTime.now();
}

/// Widget raíz
class MyApp extends StatelessWidget {
  const MyApp({super.key});
  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Trackpad App',
      theme: ThemeData.dark(),
      home: const DiscoveryPage(),
    );
  }
}

/// Pantalla de descubrimiento de hosts por UDP broadcast
class DiscoveryPage extends StatefulWidget {
  const DiscoveryPage({super.key});
  @override
  State<DiscoveryPage> createState() => _DiscoveryPageState();
}

class _DiscoveryPageState extends State<DiscoveryPage> {
  final List<ServerInfo> _servers = [];
  RawDatagramSocket? _socket;

  @override
  void initState() {
    super.initState();
    _startListening(); // Arranca la escucha UDP al iniciar
  }

  @override
  void dispose() {
    _socket?.close(); // Cierra socket al desmontar
    super.dispose();
  }

  /// Configura un RawDatagramSocket en el puerto 4568
  /// y procesa cada mensaje JSON recibido.
  void _startListening() async {
    _socket = await RawDatagramSocket.bind(InternetAddress.anyIPv4, 4568);
    _socket!.readEventsEnabled = true;
    _socket!.listen((event) {
      if (event == RawSocketEvent.read) {
        final dg = _socket!.receive();
        if (dg == null) return;
        try {
          // Decodifica JSON: { name, ip, port }
          final map = json.decode(utf8.decode(dg.data));
          final name = map['name'] as String;
          final ip = map['ip'] as String;
          final port = map['port'] as int;

          // Si ya existía, solo actualiza lastSeen; si no, lo agrega
          final existing = _servers.where((s) => s.ip == ip).toList();
          if (existing.isEmpty) {
            _servers.add(ServerInfo(name, ip, port));
          } else {
            existing.first.lastSeen = DateTime.now();
          }
          setState(() {});
        } catch (_) {
          // Ignora datos mal formados
        }
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("Descubrir hosts Trackpad")),
      body: ListView.builder(
        itemCount: _servers.length,
        itemBuilder: (ctx, i) {
          final s = _servers[i];
          return ListTile(
            title: Text(s.name), // Host name
            subtitle: Text("${s.ip}:${s.port}"), // IP:port
            trailing: Text(
              "${DateTime.now().difference(s.lastSeen).inSeconds}s",
            ), // Tiempo desde último anuncio
            onTap: () {
              // Navega a la pantalla de trackpad para este host
              Navigator.push(
                ctx,
                MaterialPageRoute(builder: (_) => TrackpadPage(server: s)),
              );
            },
          );
        },
      ),
    );
  }
}

/// Pantalla de trackpad: detecta gestos y los envía por TCP
class TrackpadPage extends StatefulWidget {
  final ServerInfo server;
  const TrackpadPage({required this.server, super.key});
  @override
  State<TrackpadPage> createState() => _TrackpadPageState();
}

class _TrackpadPageState extends State<TrackpadPage> {
  Socket? _socket; // Socket TCP
  late String _message; // Mensaje de estado o gesto
  bool _blocked = false; // Bloqueo tras cada gesto

  // Datos para multitouch
  final Map<int, Offset> _starts = {}; // Posiciones iniciales
  final Map<int, Offset> _currs = {}; // Posiciones actuales
  double? _pinch2, _pinch5; // Distancias iniciales

  // Umbrales para distinguir gestos
  static const _scrollTh = 20.0;
  static const _pinch2Th = 20.0;
  static const _pinch5Th = 10.0;
  static const _swipe4Th = 50.0;

  @override
  void initState() {
    super.initState();
    // Mensaje inicial usando la info del host
    _message = "Conectando a ${widget.server.ip}:${widget.server.port}";
    _connect(); // Inicia conexión TCP
  }

  @override
  void dispose() {
    _socket?.close(); // Cierra socket al salir
    super.dispose();
  }

  /// Conecta al servidor .NET por TCP
  void _connect() async {
    try {
      _socket = await Socket.connect(widget.server.ip, widget.server.port);
      setState(() => _message = "Conectado ✅");
    } catch (e) {
      setState(() => _message = "Error conexión: $e");
    }
  }

  /// Envía un gesto si no está bloqueado, muestra feedback y bloquea 300ms
  void _send(String gesture) {
    if (_blocked || _socket == null) return;
    _socket!.write("$gesture\n");
    setState(() => _message = gesture);
    _blocked = true;
    Future.delayed(const Duration(milliseconds: 300), () {
      _blocked = false;
    });
  }

  /// Calcula la distancia media de todos los puntos al centroide
  double _avgDistance(Map<int, Offset> m) {
    final xs = m.values.map((o) => o.dx).toList();
    final ys = m.values.map((o) => o.dy).toList();
    final cx = xs.reduce((a, b) => a + b) / xs.length;
    final cy = ys.reduce((a, b) => a + b) / ys.length;
    final cent = Offset(cx, cy);
    return m.values.map((o) => (o - cent).distance).reduce((a, b) => a + b) /
        m.length;
  }

  /// Evalúa la posición de los toques y despacha el gesto adecuado
  void _evaluate() {
    if (_blocked) return;
    final count = _currs.length;

    // 1) Pinch de 5 dedos
    if (count == 5 && _pinch5 != null) {
      final avg = _avgDistance(_currs);
      final diff = avg - _pinch5!;
      if (diff.abs() > _pinch5Th) {
        _send(diff > 0 ? "🖐️🔍 Pinch+ de 5" : "🖐️🔎 Pinch- de 5");
        return;
      }
    }

    // 2) Swipe de 4 dedos → cambio de escritorio
    //    Si mueves de izquierda→derecha (dx > 0), envía izquierda (⬅️)
    //    Si mueves de derecha→izquierda (dx < 0), envía derecha (➡️)
    if (count == 4) {
      double sumDx = 0;
      for (var id in _currs.keys) {
        sumDx += (_currs[id]!.dx - _starts[id]!.dx);
      }
      final avgDx = sumDx / 4;
      if (avgDx > _swipe4Th) {
        _send("⬅️ Cambio escritorio"); // dedo deslizándose a la derecha
      } else if (avgDx < -_swipe4Th) {
        _send("➡️ Cambio escritorio"); // dedo deslizándose a la izquierda
      }
      return;
    }

    // 3) Zoom con 2 dedos
    if (count == 2 && _pinch2 != null) {
      final ids = _currs.keys.toList();
      final a = _currs[ids[0]]!, b = _currs[ids[1]]!;
      final dist = (a - b).distance;
      final diff = dist - _pinch2!;
      if (diff.abs() > _pinch2Th) {
        _send(diff > 0 ? "🔍 Zoom+" : "🔎 Zoom-");
        return;
      }
    }

    // 4) Scroll con 2 dedos (ambos en la misma dirección)
    if (count == 2) {
      final ids = _currs.keys.toList();
      final sa = _starts[ids[0]]!, sb = _starts[ids[1]]!;
      final ca = _currs[ids[0]]!, cb = _currs[ids[1]]!;
      final dxA = ca.dx - sa.dx, dxB = cb.dx - sb.dx;
      final dyA = ca.dy - sa.dy, dyB = cb.dy - sb.dy;

      // Scroll horizontal
      if (dxA.abs() > _scrollTh &&
          dxB.abs() > _scrollTh &&
          dxA.sign == dxB.sign) {
        _send(dxA > 0 ? "➡️ Scroll H" : "⬅️ Scroll H");
        return;
      }
      // Scroll vertical
      if (dyA.abs() > _scrollTh &&
          dyB.abs() > _scrollTh &&
          dyA.sign == dyB.sign) {
        _send(dyA > 0 ? "⬇️ Scroll V" : "⬆️ Scroll V");
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.of(context).size;
    final isDark = MediaQuery.of(context).platformBrightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: const Text("Trackpad"),
        backgroundColor: isDark ? Colors.black : Colors.blue,
      ),
      body: Center(
        child: Listener(
          onPointerDown: (e) {
            // Guarda la posición inicial
            _starts[e.pointer] = e.position;
            _currs[e.pointer] = e.position;
            // Si hay 2 dedos, inicializa pinch2
            if (_currs.length == 2) {
              final ks = _currs.keys.toList();
              _pinch2 = (_currs[ks[0]]! - _currs[ks[1]]!).distance;
            }
            // Si hay 5 dedos, inicializa pinch5
            if (_currs.length == 5) {
              _pinch5 = _avgDistance(_currs);
            }
          },
          onPointerMove: (e) {
            // Actualiza posición y evalúa
            _currs[e.pointer] = e.position;
            _evaluate();
          },
          onPointerUp: (e) {
            // Elimina datos al levantar
            _starts.remove(e.pointer);
            _currs.remove(e.pointer);
            // Reinicia pinch2/5 si ya no hay esa cantidad de dedos
            if (_currs.length != 2) _pinch2 = null;
            if (_currs.length != 5) _pinch5 = null;
          },
          child: Container(
            width: size.width * 0.9,
            height: size.height * 0.7,
            decoration: BoxDecoration(
              color: isDark ? Colors.grey[800] : Colors.grey[300],
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: Colors.white, width: 2),
            ),
            alignment: Alignment.center,
            child: Text(
              _message, // Muestra el estado o gesto
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 22,
                color: isDark ? Colors.white : Colors.black,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
