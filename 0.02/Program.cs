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
SHALL DAN ANDERSON OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, 
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
        static Thread listenerThread;
        static Socket mySocket = null;

        public static void Main()
        {

            int returnCode = 0;
            
            // You can subscribe to multiple topics in one go 
            // (If your broker supports this RSMB does, mosquitto does not)
            // Our examples use one topic per request.
            //
            //int[] topicQoS = { 0, 0 };
            //String[] subTopics = { "test", "test2" };
            //int numTopics = 2;
            
            int[] topicQoS = { 0 };
            String[] subTopics = { "test/#" };
            int numTopics = 1;

            // Get broker's IP address.
            IPHostEntry hostEntry = Dns.GetHostEntry("test.mosquitto.org");
            //IPHostEntry hostEntry = Dns.GetHostEntry("192.168.1.106");
            
            // Create socket and connect to the broker's IP address and port
            mySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                mySocket.Connect(new IPEndPoint(hostEntry.AddressList[0], 1883));
            }
            catch (SocketException SE)
            {
                Debug.Print("Connection Error: " + SE.ErrorCode);
                return;
            }
        
            // Send the connect message
            // You can use UTF8 in the clientid, username and password - be careful, can be a pain
            //returnCode = NetduinoMQTT.ConnectMQTT(mySocket, "tester\u00A5", 2000, true, "roger\u00A5", "password\u00A5");
            returnCode = NetduinoMQTT.ConnectMQTT(mySocket, "tester402", 20, true, "", "");
            if (returnCode != 0)
            {
                Debug.Print("Connection Error: " + returnCode.ToString());
                return;
            }

            // Set up so that we ping the server after 1 second, then every 10 seconds
            // First time is initial delay, Second is subsequent delays
            Timer pingTimer = new Timer(new TimerCallback(pingIt), null, 1000, 10000);

            // Setup and start a new thread for the listener
            listenerThread = new Thread(mylistenerThread);
            listenerThread.Start();

            // setup our interrupt port (on-board button)
            InterruptPort button = new InterruptPort(Pins.ONBOARD_SW1, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);

            // assign our interrupt handler
            button.OnInterrupt += new NativeEventHandler(button_OnInterrupt);

            // Subscribe to our topic(s)
            returnCode = NetduinoMQTT.SubscribeMQTT(mySocket, subTopics, topicQoS, numTopics);

            //***********************************************
            // This is just some example stuff:
            //***********************************************

            // Publish a message
            NetduinoMQTT.PublishMQTT(mySocket, "test", "Testing from NetduinoMQTT to test - AAAAAAAAAAAAAAA");
            NetduinoMQTT.PublishMQTT(mySocket, "test", "Testing from NetduinoMQTT to test - BBBBBBBBBBBBBBB");
           
            // Subscribe to "test/two"
            //subTopics[0] = "test/two";
            //returnCode = NetduinoMQTT.SubscribeMQTT(mySocket, subTopics, topicQoS, numTopics);
            
            // Send a message to "test/two"
            //NetduinoMQTT.PublishMQTT(mySocket, "test/two", "Testing from NetduinoMQTT to test/two");
           
             // Unsubscribe from "test/two"
            //returnCode = NetduinoMQTT.UnsubscribeMQTT(mySocket, subTopics, topicQoS, numTopics);
           
            // Send a message to "test/two"
            //NetduinoMQTT.PublishMQTT(mySocket, "test/two", "Testing again from NetduinoMQTT to test/two"); // Shouldn't see this one
            
            // Subscribe to "test/#"
            //subTopics[0] = "test/#";
            //returnCode = NetduinoMQTT.SubscribeMQTT(mySocket, subTopics, topicQoS, numTopics);
 
            // Send a message to "test/two"
            //NetduinoMQTT.PublishMQTT(mySocket, "test/three/one", "Testing again from NetduinoMQTT to test/three");
            
            // go to sleep until the interrupt or the timer wakes us 
            // (mylistenerThread is in a seperate thread that continues)
            Thread.Sleep(Timeout.Infinite);
        }

        // the interrupt handler for the button
        static void button_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            // Send our message
            NetduinoMQTT.PublishMQTT(mySocket, "test", "Ow! Quit it!");
            // Send a message to "test/two"
            NetduinoMQTT.PublishMQTT(mySocket, "test/three/one", "Testing again from NetduinoMQTT to test/three");
            NetduinoMQTT.PublishMQTT(mySocket, "test/two/one", "Testing again from NetduinoMQTT to test/two");
            NetduinoMQTT.PublishMQTT(mySocket, "test/four/one", "Testing again from NetduinoMQTT to test/four");
            NetduinoMQTT.PublishMQTT(mySocket, "test/five/one", "Testing again from NetduinoMQTT to test/fivr");
            return;
        }

        // The thread that listens for inbound messages
        private static void mylistenerThread()
        {
                NetduinoMQTT.listen(mySocket);
        }

        // The function that the timer calls to ping the server
        // Our keep alive is 15 seconds - we ping again every 10. 
        // So we should live forever.
        static void pingIt(object o)
        {
            Debug.Print("pingIT");
            NetduinoMQTT.PingMQTT(mySocket);
        }
    }
}
