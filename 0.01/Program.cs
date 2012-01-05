using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using Netduino_MQTT_Client_Library;

namespace myNetduinoMQTT
{
    public class Program
    {
        public static void Main()
        {

            Socket mySocket = ConnectSocket("192.168.1.106",1883);
            NetduinoMQTT.ConnectMQTT(mySocket, "tester123", 10);
            NetduinoMQTT.PublishMQTT(mySocket, "test", "testme12");
            Thread.Sleep(1000);
            NetduinoMQTT.DisconnectMQTT(mySocket);
            
        }
        
        // From Sample Code (SockClient.cs) Copyright Microsoft
        public static Socket ConnectSocket(String server, Int32 port)
        {
            // Get server's IP address.
            IPHostEntry hostEntry = Dns.GetHostEntry(server);

            // Create socket and connect to the server's IP address and port
            Socket socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(hostEntry.AddressList[0], port));
            return socket;
        }
        // End of Microsoft Copyright sample code


    }
}
