import 'package:flutter/material.dart';
import 'dart:async'; // Para Future.delayed
import 'dart:math'; // Para cálculos de distancia

void main() {
  runApp(const MyApp());
}

/// Widget raíz de la aplicación: define tema y pantalla inicial
class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Trackpad Demo',
      theme: ThemeData.dark(), // Usamos tema oscuro
      home: const TrackpadScreen(), // Carga la pantalla principal
    );
  }
}

/// Pantalla principal con estado, aquí gestionamos todos los gestos
class TrackpadScreen extends StatefulWidget {
  const TrackpadScreen({super.key});
  @override
  State<TrackpadScreen> createState() => _TrackpadScreenState();
}

class _TrackpadScreenState extends State<TrackpadScreen> {
  // ----------------- ESTADO VISIBLE -----------------
  String _mensaje = "Usa el trackpad"; // Texto que ve el usuario

  // --------------- ESTADO DE DETECCIÓN ---------------
  final Map<int, Offset> _startPositions =
      {}; // Posición inicial de cada dedo (pointer ID)
  final Map<int, Offset> _currentPositions = {}; // Posición actual de cada dedo
  double? _initialPinchDistance2; // Distancia inicial entre 2 dedos
  double?
  _initialPinchDistance5; // Distancia inicial media de 5 dedos al centro
  bool _bloqueado = false; // Bloquea gestos sucesivos breves

  // --------------- UMBRALES Y TIEMPOS ---------------
  static const double _scrollThreshold =
      20.0; // px mínimos para detectar scroll
  static const double _pinch2Threshold =
      20.0; // px mínimos para pinch/zoom con 2 dedos
  static const double _pinch5Threshold =
      10.0; // px mínimos para pinch con 5 dedos
  static const double _swipe4Threshold =
      50.0; // px mínimos para swipe de 4 dedos
  static const Duration _lockDuration = Duration(
    milliseconds: 300,
  ); // bloqueo 300ms

  /// Centraliza la acción: muestra mensaje y bloquea nuevos gestos temporalmente
  Future<void> _ejecutarAccion(String texto) async {
    if (_bloqueado) return; // Si ya está bloqueado no hace nada
    setState(() => _mensaje = texto); // Actualiza el texto en pantalla
    print(texto); // También lo imprime en consola
    _bloqueado = true; // Activa el bloqueo
    await Future.delayed(_lockDuration);
    _bloqueado = false; // Quita el bloqueo
  }

  /// Calcula distancia euclidiana entre dos puntos
  double _distance(Offset a, Offset b) => (a - b).distance;

  /// Calcula la distancia media de un conjunto de puntos al centroide
  double _averageDistanceToCentroid(Map<int, Offset> positions) {
    // Calcula centroide
    final xs = positions.values.map((o) => o.dx).toList();
    final ys = positions.values.map((o) => o.dy).toList();
    final centroid = Offset(
      xs.reduce((a, b) => a + b) / xs.length,
      ys.reduce((a, b) => a + b) / ys.length,
    );
    // Distancia media
    final total = positions.values
        .map((o) => _distance(o, centroid))
        .reduce((a, b) => a + b);
    return total / positions.length;
  }

  /// Evalúa gestos multitáctiles según número de dedos
  void _evaluarMultiTouch() {
    if (_bloqueado) return; // Si está en bloqueo, no procesa
    final count = _currentPositions.length; // Cuántos dedos tocan

    // ------- 1) PINCH de 5 dedos -------
    if (count == 5 && _initialPinchDistance5 != null) {
      final currAvg = _averageDistanceToCentroid(_currentPositions);
      final diff = currAvg - (_initialPinchDistance5!);
      if (diff.abs() > _pinch5Threshold) {
        if (diff > 0) {
          // Cinco dedos separándose: Pinch+ (como abrir Mission Control)
          _ejecutarAccion("🖐️🔍 Pinch+ de 5 dedos detectado");
        } else {
          // Cinco dedos acercándose: Pinch- (como cerrar Mission Control)
          _ejecutarAccion("🖐️🔎 Pinch- de 5 dedos detectado");
        }
        return;
      }
    }

    // ------- 2) SWIPE de 4 dedos -------
    if (count == 4) {
      // Calculamos desplazamiento promedio en X de los 4 dedos
      double sumDx = 0;
      for (var id in _currentPositions.keys) {
        sumDx += (_currentPositions[id]!.dx - _startPositions[id]!.dx);
      }
      final avgDx = sumDx / 4;
      if (avgDx > _swipe4Threshold) {
        _ejecutarAccion("Cambio de escritorio →");
      } else if (avgDx < -_swipe4Threshold) {
        _ejecutarAccion("Cambio de escritorio ←");
      }
      return;
    }

    // ------- 3) ZOOM / PINCH con 2 dedos -------
    if (count == 2 && _initialPinchDistance2 != null) {
      final ids = _currentPositions.keys.toList();
      final aCurr = _currentPositions[ids[0]]!;
      final bCurr = _currentPositions[ids[1]]!;
      final currDist = _distance(aCurr, bCurr);
      final diff = currDist - (_initialPinchDistance2!);
      if (diff.abs() > _pinch2Threshold) {
        if (diff > 0) {
          // Dos dedos separándose: Zoom+
          _ejecutarAccion("🔍 Zoom+ detectado");
        } else {
          // Dos dedos acercándose: Zoom-
          _ejecutarAccion("🔎 Zoom- detectado");
        }
        return;
      }
    }

    // ------- 4) SCROLL con 2 dedos -------
    if (count == 2) {
      final ids = _currentPositions.keys.toList();
      final startA = _startPositions[ids[0]]!;
      final startB = _startPositions[ids[1]]!;
      final currA = _currentPositions[ids[0]]!;
      final currB = _currentPositions[ids[1]]!;

      final dxA = currA.dx - startA.dx;
      final dxB = currB.dx - startB.dx;
      final dyA = currA.dy - startA.dy;
      final dyB = currB.dy - startB.dy;

      // Scroll horizontal: ambos en misma dirección X
      if (dxA.abs() > _scrollThreshold &&
          dxB.abs() > _scrollThreshold &&
          dxA.sign == dxB.sign) {
        if (dxA > 0)
          _ejecutarAccion("⇢ Scroll horizontal derecha");
        else
          _ejecutarAccion("⇠ Scroll horizontal izquierda");
        return;
      }
      // Scroll vertical: ambos en misma dirección Y
      if (dyA.abs() > _scrollThreshold &&
          dyB.abs() > _scrollThreshold &&
          dyA.sign == dyB.sign) {
        if (dyA > 0)
          _ejecutarAccion("⇣ Scroll vertical abajo");
        else
          _ejecutarAccion("⇡ Scroll vertical arriba");
        return;
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    // Tamaño de pantalla para adaptar el área del trackpad
    final screenSize = MediaQuery.of(context).size;
    // Detecta modo oscuro/claro
    final isDarkMode =
        MediaQuery.of(context).platformBrightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: const Text("Tablet como Trackpad"),
        backgroundColor: isDarkMode ? Colors.black : Colors.blue,
      ),
      body: Center(
        child: Listener(
          // Cuando un dedo toca:
          onPointerDown: (event) {
            // Guardamos posición inicial y actual para ese dedo
            _startPositions[event.pointer] = event.position;
            _currentPositions[event.pointer] = event.position;
            // Si justo hay 2 dedos, inicializamos distancia de pinch2
            if (_currentPositions.length == 2) {
              final ids = _currentPositions.keys.toList();
              _initialPinchDistance2 = _distance(
                _currentPositions[ids[0]]!,
                _currentPositions[ids[1]]!,
              );
            }
            // Si justo hay 5 dedos, inicializamos distancia media pinch5
            if (_currentPositions.length == 5) {
              _initialPinchDistance5 = _averageDistanceToCentroid(
                _currentPositions,
              );
            }
          },
          // Cuando un dedo se mueve:
          onPointerMove: (event) {
            // Actualizamos la posición de ese dedo
            _currentPositions[event.pointer] = event.position;
            // Evaluamos todos los gestos multitáctiles
            _evaluarMultiTouch();
          },
          // Cuando un dedo levanta:
          onPointerUp: (event) {
            // Lo quitamos de ambas colecciones
            _startPositions.remove(event.pointer);
            _currentPositions.remove(event.pointer);
            // Si ya no hay 2 dedos, reseteamos pinch2
            if (_currentPositions.length != 2) {
              _initialPinchDistance2 = null;
            }
            // Si ya no hay 5 dedos, reseteamos pinch5
            if (_currentPositions.length != 5) {
              _initialPinchDistance5 = null;
            }
          },

          // Zona visual del trackpad:
          child: Container(
            width: screenSize.width * 0.9, // 90% ancho
            height: screenSize.height * 0.7, // 70% alto
            decoration: BoxDecoration(
              color: isDarkMode ? Colors.grey[800] : Colors.grey[300],
              borderRadius: BorderRadius.circular(20),
              border: Border.all(color: Colors.white, width: 2),
            ),
            alignment: Alignment.center,
            child: Text(
              _mensaje, // Mensaje dinámico
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 22,
                color: isDarkMode ? Colors.white : Colors.black,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
