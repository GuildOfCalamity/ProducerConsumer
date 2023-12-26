using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProducerConsumer;

/// <summary>
/// This class does not incorporate any actual serial port calls. 
/// It only exists to simulate a simple serial device communication flow.
/// </summary>
public class SerialEmulator
{
    public int Delay { get; private set; }
    public bool Connected { get; private set; }
    public string Device { get; private set; }
    public int Baud { get; private set; }
    public int DataBits { get; private set; }
    public string Parity { get; private set; }
    public int StopBits { get; private set; }

    public SerialEmulator()
    {
        Device = Utils.GetRandomKey();
        Baud = 9600;
        DataBits = 8;
        Parity = "N";
        StopBits = 1;
        Delay = 200; // milliseconds
    }

    public SerialEmulator(string device, int baud, int dataBits, string parity, int stopBits, int delay)
    {
        Device = device;
        Baud = baud;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
        Delay = delay;
    }

    /// <summary>
    /// If we are already connected then we return the <see cref="Connected"/> status.
    /// </summary>
    /// <returns>true if connected, false otherwise</returns>
    public bool Connect()
    {
        if (Connected)
            return Connected;

        // Fake a connection attempt.
        if (Utils.CoinFlip())
            Connected = true;
        else
            Connected = false;

        Thread.Sleep(Delay);

        return Connected;
    }

    public void Disconnect()
    {
        if (Connected)
           Connected = false;

        Thread.Sleep(Delay);
    }

    public bool SendData(string data)
    {
        if (Connected)
        {
            Debug.WriteLine($"Sending \"{data}\" to \"{Device}\"...");
            Thread.Sleep(Delay);
            return true;
        }
        else
        {
            Debug.WriteLine($"Could not send \"{data}\" to \"{Device}\".");
            Thread.Sleep(Delay);
            return false;
        }
    }

    public string ReceiveData()
    {
        if (Connected)
        {
            Debug.WriteLine($"Receiving data from \"{Device}\"...");
            Thread.Sleep(Delay);
            return Utils.GetRandomKey(32);
        }
        else
        {
            Debug.WriteLine($"\"{Device}\" is not connected.");
            Thread.Sleep(Delay);
            return string.Empty;
        }
    }
}
