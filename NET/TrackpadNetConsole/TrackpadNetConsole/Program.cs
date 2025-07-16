// Program.cs
// Aplicación .NET 9 para recibir gestos por TCP y ejecutar acciones de sistema.
// Incluye UDP broadcast para descubrimiento y servidor TCP para manejar gestos.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Para simular teclado y ratón en Windows
using WindowsInput;
using WindowsInput.Native;

// Para detectar el sistema operativo en tiempo de ejecución
using System.Runtime.InteropServices;

// Para ejecutar AppleScript en macOS
using System.Diagnostics;

class Program
{
    // Puerto UDP para discovery (broadcast)
    const int DiscoverPort = 4568;
    // Puerto TCP donde escucharemos conexiones de Flutter
    const int TcpPort      = 4567;

    static async Task Main()
    {
        // Iniciamos en paralelo 1) broadcast UDP  2) servidor TCP
        _ = BroadcastDiscovery();   // Corre en background
        await StartTcpServer();     // Atiende conexiones TCP
    }

    /// <summary>
    /// 1) Envía cada 2 segundos un paquete UDP broadcast con:
    ///    { name, ip, port } para que las apps Flutter lo descubran.
    /// </summary>
    static async Task BroadcastDiscovery()
    {
        using var udp = new UdpClient { EnableBroadcast = true };  // Cliente UDP en modo broadcast

        string host = Dns.GetHostName();                           // Nombre de la máquina
        string ip   = GetLocalIPv4();                              // IP local IPv4 no loopback

        var ep = new IPEndPoint(IPAddress.Broadcast, DiscoverPort); // Endpoint de broadcast

        Console.WriteLine($"📢 Broadcast UDP: {host}@{ip}:{TcpPort}");
        while (true)
        {
            // Serializa la información a JSON
            var info  = new { name = host, ip = ip, port = TcpPort };
            var json  = JsonSerializer.Serialize(info);
            var data  = Encoding.UTF8.GetBytes(json);

            await udp.SendAsync(data, data.Length, ep);             // Envía paquete
            await Task.Delay(2000);                                 // Espera 2s
        }
    }

    /// <summary>
    /// 2) Inicia un servidor TCP en TcpPort, acepta clientes y
    ///    despacha cada conexión a HandleClient.
    /// </summary>
    static async Task StartTcpServer()
    {
        var listener = new TcpListener(IPAddress.Any, TcpPort);    // Listener en cualquier IP
        listener.Start();
        Console.WriteLine($"🎧 TCP escuchando en puerto {TcpPort}");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();    // Acepta nuevo cliente
            Console.WriteLine("🔗 Cliente conectado");
            _ = HandleClient(client);                              // Lo maneja en background
        }
    }

    /// <summary>
    /// 3) Lee datos del cliente línea a línea y llama a PerformAction.
    /// </summary>
    static async Task HandleClient(TcpClient client)
    {
        using var stream = client.GetStream();
        var buffer = new byte[1024];

        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length); // Lee hasta 1024 bytes
            }
            catch
            {
                break;                                                // Error de red
            }

            if (bytesRead == 0) break;                                // Cliente cerró

            // Convierte bytes a string y trim de saltos
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            Console.WriteLine($"📨 Recibido: {message}");

            PerformAction(message);                                  // Ejecuta la acción
        }

        Console.WriteLine("🔌 Cliente desconectado");
    }

    /// <summary>
    /// 4) Mapea los mensajes de gesto a atajos de teclado/ratón según SO.
    ///    El cambio de escritorio invierte el sentido tal como trackpads profesionales.
    /// </summary>
    static void PerformAction(string msg)
    {
        var sim = new InputSimulator();                            // Para Windows
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isMac     = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        switch (msg)
        {
            // ➡️ Swipe a la derecha → ir al escritorio anterior (flecha izquierda)
            case "➡️ Cambio escritorio":
                if (isWindows)
                {
                    sim.Keyboard.ModifiedKeyStroke(
                        new[] { VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL },
                        VirtualKeyCode.LEFT);
                }
                else if (isMac)
                {
                    // En macOS, solo control para cambio de escritorio (no command)
                    RunAppleScript(
                      "tell application \"System Events\" to key code 124 using {control down}"
                    );
                }
                break;

            // ⬅️ Swipe a la izquierda → ir al escritorio siguiente (flecha derecha)
            case "⬅️ Cambio escritorio":
                if (isWindows)
                {
                    sim.Keyboard.ModifiedKeyStroke(
                        new[] { VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL },
                        VirtualKeyCode.RIGHT);
                }
                else if (isMac)
                {
                    RunAppleScript(
                      "tell application \"System Events\" to key code 123 using {control down}"
                    );
                }
                break;

            // ➡️ Scroll H derecha
            case "➡️ Scroll H":
                if (isWindows) sim.Mouse.HorizontalScroll(1);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to key code 124"
                    );
                break;

            // ⬅️ Scroll H izquierda
            case "⬅️ Scroll H":
                if (isWindows) sim.Mouse.HorizontalScroll(-1);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to key code 123"
                    );
                break;

            // ⬇️ Scroll V abajo
            case "⬇️ Scroll V":
                if (isWindows) sim.Mouse.VerticalScroll(-1);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to key code 125"
                    );
                break;

            // ⬆️ Scroll V arriba
            case "⬆️ Scroll V":
                if (isWindows) sim.Mouse.VerticalScroll(1);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to key code 126"
                    );
                break;

            // 🔍 Zoom+ (Ctrl/Cmd +)
            case "🔍 Zoom+":
                if (isWindows)
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.OEM_PLUS);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to keystroke \"+\" using {command down}"
                    );
                break;

            // 🔎 Zoom- (Ctrl/Cmd -)
            case "🔎 Zoom-":
                if (isWindows)
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.OEM_MINUS);
                else if (isMac)
                    RunAppleScript(
                      "tell application \"System Events\" to keystroke \"-\" using {command down}"
                    );
                break;

            // 🖐️🔍 Pinch+ de 5 dedos → Task View / Mission Control
            case "🖐️🔍 Pinch+ de 5":
                if (isWindows)
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
                else if (isMac)
                    RunAppleScript(
                      // Escape (key code 53) cierra Mission Control
                      "tell application \"System Events\" to key code 103"
                    );
                break;

            // 🖐️🔎 Pinch- de 5 dedos → cerrar Task View / Mission Control
            case "🖐️🔎 Pinch- de 5":
                if (isWindows)
                    sim.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);
                else if (isMac)
                    RunAppleScript(
                      // F4 (key code 118) abre el Launchpad (menú de aplicaciones)
                      "tell application \"System Events\" to key code 130"
                    );
                break;
        }
    }

    /// <summary>
    /// Ejecuta un comando AppleScript en macOS (una sola línea).
    /// </summary>
    static void RunAppleScript(string script)
    {
        // Escapa las comillas dobles para AppleScript
        string safeScript = script.Replace("\"", "\\\"");
        Process.Start(new ProcessStartInfo
        {
            FileName               = "osascript",      // Ejecutable de AppleScript
            Arguments              = $"-e \"{safeScript}\"", // Línea de script escapada
            RedirectStandardOutput = true,
            UseShellExecute        = false
        });
    }

    /// <summary>
    /// Obtiene la IP IPv4 local (no loopback).
    /// </summary>
    static string GetLocalIPv4()
    {
        foreach (var addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (addr.AddressFamily == AddressFamily.InterNetwork
             && !IPAddress.IsLoopback(addr))
                return addr.ToString();
        }
        // Fallback si algo falla
        return "127.0.0.1";
    }
}
