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
            // This is where you would do your work  
            Socket mySocket = NetduinoMQTT.ConnectSocket("192.168.1.1",1883);  
            NetduinoMQTT.ConnectMQTT(mySocket,"tester123");  
            NetduinoMQTT.PublishMQTT(mySocket, "test", "testme12");  
            Thread.Sleep(1000);  
            NetduinoMQTT.DisconnectMQTT(mySocket);  
        }  
    }  
}