using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Parámetros de red
        const int discoverPort = 4568;      // Puerto para discovery UDP
        const int tcpPort = 4567;           // Puerto de tu servidor TCP
        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, discoverPort);

        // Obtenemos datos del host
        string hostName = Dns.GetHostName();
        string localIp = GetLocalIPAddress();

        // Creamos socket UDP para broadcast
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        Console.WriteLine($"🚀 Iniciando broadcast de descubrimiento en UDP port {discoverPort}");
        Console.WriteLine($"   Host: {hostName}, IP: {localIp}, TCP Port: {tcpPort}");

        // Preparamos JSON de anuncio
        var info = new { name = hostName, ip = localIp, port = tcpPort };
        string json = JsonSerializer.Serialize(info);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        // En bucle, cada 2 segundos enviamos el anuncio
        while (true)
        {
            await udp.SendAsync(payload, payload.Length, broadcastEndpoint);
            Console.WriteLine($"📢 Broadcast enviado: {json}");
            await Task.Delay(2000);
        }
    }

    // Helper: obtiene la IPv4 no loopback
    static string GetLocalIPAddress()
    {
        foreach (var ni in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ni.AddressFamily == AddressFamily.InterNetwork)
                return ni.ToString();
        }
        return "127.0.0.1";
    }
}
