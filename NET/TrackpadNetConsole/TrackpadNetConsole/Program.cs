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
        // Iniciamos en paralelo 1) broadcast UDP 2) servidor TCP
        _ = BroadcastDiscovery();   // Corre en background
        await StartTcpServer();     // Atiende conexiones TCP
    }

    /// <summary>
    /// Envía cada 2 segundos un paquete UDP broadcast con
    /// { name, ip, port } para que las apps Flutter lo descubran.
    /// </summary>
    static async Task BroadcastDiscovery()
    {
        // Creamos cliente UDP con broadcast habilitado
        using var udp = new UdpClient { EnableBroadcast = true };

        // Obtenemos hostname e IP local
        string host = Dns.GetHostName();
        string ip   = GetLocalIPv4();

        // Endpoint de broadcast: 255.255.255.255:DiscoverPort
        var ep = new IPEndPoint(IPAddress.Broadcast, DiscoverPort);

        Console.WriteLine($"📢 Broadcast UDP: {host}@{ip}:{TcpPort}");
        while (true)
        {
            // Creamos objeto con info y lo serializamos a JSON
            var info = new { name = host, ip = ip, port = TcpPort };
            var json = JsonSerializer.Serialize(info);
            var data = Encoding.UTF8.GetBytes(json);

            // Enviamos el JSON por UDP broadcast
            await udp.SendAsync(data, data.Length, ep);

            // Esperamos 2 segundos antes de repetir
            await Task.Delay(2000);
        }
    }

    /// <summary>
    /// Inicia un servidor TCP en TcpPort, acepta clientes y
    /// despacha cada conexión a HandleClient.
    /// </summary>
    static async Task StartTcpServer()
    {
        // Listener en cualquier IP local y el puerto TcpPort
        var listener = new TcpListener(IPAddress.Any, TcpPort);
        listener.Start();
        Console.WriteLine($"🎧 TCP escuchando en puerto {TcpPort}");

        while (true)
        {
            // Espera a que un cliente se conecte
            var client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("🔗 Cliente conectado");

            // Maneja cada cliente de forma asíncrona
            _ = HandleClient(client);
        }
    }

    /// <summary>
    /// Lee datos del cliente línea a línea y llama a PerformAction.
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
                // Lee hasta 1024 bytes
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            }
            catch
            {
                // Error de red o desconexión
                break;
            }

            // Si no se leyeron bytes, el cliente cerró la conexión
            if (bytesRead == 0) break;

            // Convertimos bytes a string y eliminamos salto de línea
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
            Console.WriteLine($"📨 Recibido: {message}");

            // Interpretamos y ejecutamos la acción
            PerformAction(message);
        }

        Console.WriteLine("🔌 Cliente desconectado");
    }

    /// <summary>
    /// Mapea los mensajes de gesto a atajos de teclado/ratón según SO.
    /// El cambio de escritorio invierte el sentido tal como trackpads profesionales.
    /// </summary>
    static void PerformAction(string msg)
    {
        // InputSimulator para Windows
        var sim = new InputSimulator();

        // Detecta el sistema operativo actual
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isMac     = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        switch (msg)
        {
            // Swipe hacia la derecha: mover al escritorio anterior (flecha izquierda)
            case "➡️ Cambio escritorio":
                if (isWindows)
                {
                    // Win+Ctrl+LeftArrow
                    sim.Keyboard.ModifiedKeyStroke(
                        new[] { VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL },
                        VirtualKeyCode.LEFT);
                }
                else if (isMac)
                {
                    // Control+Command+LeftArrow via AppleScript
                    RunAppleScript(
                        "tell application \"System Events\" to key code 123 using {control down, command down}"
                    );
                }
                break;

            // Swipe hacia la izquierda: mover al escritorio siguiente (flecha derecha)
            case "⬅️ Cambio escritorio":
                if (isWindows)
                {
                    // Win+Ctrl+RightArrow
                    sim.Keyboard.ModifiedKeyStroke(
                        new[] { VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL },
                        VirtualKeyCode.RIGHT);
                }
                else if (isMac)
                {
                    RunAppleScript(
                        "tell application \"System Events\" to key code 124 using {control down, command down}"
                    );
                }
                break;

            // Scroll horizontal derecha
            case "➡️ Scroll H":
                if (isWindows)
                    sim.Mouse.HorizontalScroll(1);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to do shell script \"osascript -e 'key code 124'\"");
                break;

            // Scroll horizontal izquierda
            case "⬅️ Scroll H":
                if (isWindows)
                    sim.Mouse.HorizontalScroll(-1);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to key code 123");
                break;

            // Scroll vertical abajo
            case "⬇️ Scroll V":
                if (isWindows)
                    sim.Mouse.VerticalScroll(-1);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to key code 125");
                break;

            // Scroll vertical arriba
            case "⬆️ Scroll V":
                if (isWindows)
                    sim.Mouse.VerticalScroll(1);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to key code 126");
                break;

            // Zoom in (2 dedos separándose)
            case "🔍 Zoom+":
                if (isWindows)
                    // Ctrl+'+' 
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.OEM_PLUS);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to keystroke \"+\" using {command down}");
                break;

            // Zoom out (2 dedos acercándose)
            case "🔎 Zoom-":
                if (isWindows)
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.OEM_MINUS);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to keystroke \"-\" using {command down}");
                break;

            // Pinch+ de 5 dedos: abrir Task View / Mission Control
            case "🖐️🔍 Pinch+ de 5":
                if (isWindows)
                    sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to key code 48 using {control down, command down}");
                break;

            // Pinch- de 5 dedos: cerrar Task View / salir de Mission Control
            case "🖐️🔎 Pinch- de 5":
                if (isWindows)
                    sim.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);
                else if (isMac)
                    RunAppleScript("tell application \"System Events\" to key code 53");
                break;
        }
    }

    /// <summary>
    /// Ejecuta un comando AppleScript en macOS.
    /// </summary>
    static void RunAppleScript(string script)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName               = "osascript",      // Ejecutable de AppleScript
            Arguments              = $"-e \"{script}\"",// Código a ejecutar
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
            if (addr.AddressFamily == AddressFamily.InterNetwork)
                return addr.ToString();
        }
        // Fallback si no se encuentra
        return "127.0.0.1";
    }
}
