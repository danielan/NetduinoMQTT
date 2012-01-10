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
            
            // setup our interrupt port (on-board button)
            InterruptPort button = new InterruptPort(Pins.ONBOARD_SW1, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);

            // assign our interrupt handler
            button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);

            // go to sleep until the interrupt wakes us (saves power)
            Thread.Sleep(Timeout.Infinite);
        }

        // the interrupt handler 
        static void button_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            int returnCode = 0;
            int connectionError = 0;
            // Get broker's IP address.
            IPHostEntry hostEntry = Dns.GetHostEntry("192.168.1.106");
            // Create socket and connect to the broker's IP address and port
            Socket mySocket = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);
            try
            {
                mySocket.Connect(new IPEndPoint(hostEntry.AddressList[0], 1883));
            }
            catch (SocketException SE)
            {
                Debug.Print("Connection Error");
                connectionError = 1;
            }
            if (connectionError != 1)
            {
                // Send the connect message
                returnCode = NetduinoMQTT.ConnectMQTT(mySocket, "tester\u00A5", 2, true, "roger\u00A5", "password\u00A5");
                if (returnCode != 0)
                {
                    Debug.Print("Connection Error:");
                    Debug.Print(returnCode.ToString());
                }
                else
                {
                    // Send our message
                    NetduinoMQTT.PublishMQTT(mySocket, "test", "Ow! Quit it!");

                    for (int i = 0; i < 11; i++)
                    {
                        returnCode = NetduinoMQTT.PingMQTT(mySocket);
                        if (returnCode == 0)
                        {
                            Debug.Print("Ping Received");
                            Thread.Sleep(1000);
                        }
                    }
                    Thread.Sleep(3000);
                    // Send the disconnect message
                    NetduinoMQTT.DisconnectMQTT(mySocket);
                }
                // Close the socket
                mySocket.Close();
            }
        }
    }
}
