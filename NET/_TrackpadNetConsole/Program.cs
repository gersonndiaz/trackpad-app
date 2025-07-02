using System;
using System.Threading.Tasks;
using InTheHand.Bluetooth; // Biblioteca multiplataforma para BLE

Console.WriteLine("🔍 Escaneando dispositivos Bluetooth LE...");

var options = new RequestDeviceOptions
{
    AcceptAllDevices = true
};

BluetoothDevice? device = null;

try
{
    device = await Bluetooth.RequestDeviceAsync(options);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error al buscar dispositivo: {ex.Message}");
    return;
}

if (device == null)
{
    Console.WriteLine("⚠️ No se seleccionó ningún dispositivo.");
    return;
}

Console.WriteLine($"✅ Dispositivo seleccionado: {device.Name} ({device.Id})");

// Corrección: no asignamos, solo esperamos
await device.Gatt.ConnectAsync();

// Ahora usamos device.Gatt directamente
if (!device.Gatt.IsConnected)
{
    Console.WriteLine("❌ No se pudo conectar.");
    return;
}

Console.WriteLine("✅ Conectado. Explorando servicios...");

try
{
    var services = await device.Gatt.GetPrimaryServicesAsync();
    foreach (var service in services)
    {
        Console.WriteLine($"🔧 Servicio: {service.Uuid}");

        try
        {
            var characteristics = await service.GetCharacteristicsAsync();
            foreach (var characteristic in characteristics)
            {
                Console.WriteLine($"  🧬 Característica: {characteristic.Uuid}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️ Error al obtener características: {ex.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error al explorar servicios: {ex.Message}");
}

Console.WriteLine("🎯 Fin de exploración.");

// Si BluetoothDevice implementa IDisposable, descomenta la siguiente línea:
// device?.Dispose();
