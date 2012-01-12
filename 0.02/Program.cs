/*
 
Copyright 2011-2012 Dan Anderson. All rights reserved.
Redistribution and use in source and binary forms, with or without modification, 
are permitted provided that the following conditions are met:

   1. Redistributions of source code must retain the above copyright notice, 
      this list of conditions and the following disclaimer.

   2. Redistributions in binary form must reproduce the above copyright notice, 
      this list of conditions and the following disclaimer in the documentation 
      and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY DAN ANDERSON ''AS IS'' AND ANY EXPRESS OR IMPLIED
WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT 
SHALL Dan Anderson OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR 
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE.

 */

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
