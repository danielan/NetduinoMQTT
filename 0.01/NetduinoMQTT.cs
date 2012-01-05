using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.SPOT;
using Socket = System.Net.Sockets.Socket;

namespace Netduino_MQTT_Client_Library
{
    static class Constants
    {
    
        // TODO: Add other constants so we don't have "magic" numbers
        public const int MQTTPROTOCOLVERSION = 3;
        public const int MAXLENGTH = 268435455; // 256MB
        public const int ALIVE = 0;
        public const int DEAD = 1;

        // Error Codes
        public const int CLIENTID_LENGTH_ERROR = -1;
        public const int KEEPALIVE_LENGTH_ERROR = -2;
        public const int MESSAGE_LENGTH_ERROR = -3;
        public const int TOPIC_LENGTH_ERROR = -4;
        public const int TOPIC_WILDCARD_ERROR = -5;
    }

    public static class NetduinoMQTT
    {

        public static int ConnectMQTT(Socket mySocket, String clientID, int keepAlive = 20)
        {

            // TODO: detect failure - maybe QoS 1?
            int index=0;
            byte[] buffer = null;

            // Some Error Checking
            // ClientID improperly sized
            if ((clientID.Length > 23) || (clientID.Length < 1))
                return Constants.CLIENTID_LENGTH_ERROR;
            // KeepAlive out of bounds
            if ((keepAlive > 65535) || (keepAlive < 0))
                return Constants.KEEPALIVE_LENGTH_ERROR;

            // Build our buffer
            buffer = new byte[16 + clientID.Length];
            
            // Fixed Header (2.1)
            buffer[index++] = 0x10; // Connect Code
            buffer[index++] = (byte)(14 + clientID.Length); // Remaining Length
            // End Fixed Header
            
            // Connect (3.1) 
            // Protocol Name
            buffer[index++] = 0;          // String (MQIsdp) Length MSB - always 6 so, zeroed
            buffer[index++] = 6;          // Length LSB
            buffer[index++] = (byte)'M';  // M
            buffer[index++] = (byte)'Q';  // Q
            buffer[index++] = (byte)'I';  // I
            buffer[index++] = (byte)'s';  // s
            buffer[index++] = (byte)'d';  // d
            buffer[index++] = (byte)'p';  // p
            
            // Protocol Version
            buffer[index++] = Constants.MQTTPROTOCOLVERSION;
            
            // Connect Flags - Right now, just "clean session" is set
            // TODO: add in support for other options (esp username and passwd)
            // TODO: re-read the spec to figure out what the "will" stuff is about
            buffer[index++] = 0x02;
            
            // Keep alive (defaulted to 20 seconds above)
            buffer[index++] = (byte)(keepAlive / 255);   // Keep Alive MSB
            buffer[index++] = (byte)(keepAlive % 255);   // Keep Alive LSB
            
            // ClientID 0 - Never > 23 so we'll leave the MSB zeroed
            buffer[index++] = 0;                       // Length MSB
            buffer[index++] = (byte)clientID.Length;   // Length LSB
            for (var i = 0; i < clientID.Length; i++) 
            {
                buffer[index++] = (byte)clientID[i]; 
            }
            return mySocket.Send(buffer, index, 0); ;
        }

        // Publish a message to a broker (3.3)
        public static int PublishMQTT(Socket mySocket, String topic, String message)
        {

            int index = 0;
            int digit = 0;
            int tmp = 0;
            int extraFixedBytes = 0;
            int remainingLength = 0;
            int bufferSize = 0;
            byte[] buffer = null;

            // Some error checking
            if ((topic.Length > 32767)||(topic.Length < 1))
                return Constants.TOPIC_LENGTH_ERROR;
            
            if ((topic.IndexOf('#')!=-1)||(topic.IndexOf('+')!=-1))
                return Constants.TOPIC_WILDCARD_ERROR;              

            // Calculate the size for the buffer
            bufferSize = topic.Length + message.Length;
            bufferSize++;   // Space for variable header Length MSB
            bufferSize++;   // Space for variable header Length LSB
            tmp = bufferSize; // tmp var - we don't want to mess with buffersize
            
            // Add space for each byte we need in the fixed header to store the length
            while (tmp > 0)
            {
                bufferSize++;
                extraFixedBytes++;
                tmp = tmp / 128;
            };
            remainingLength = bufferSize - extraFixedBytes;
            // Build buffer for message
            buffer = new byte[bufferSize + 1]; // account for fixed header byte 1

            // Check that remaining length will fit into 4 encoded bytes
            // Account for variable fixed header
            if (remainingLength > Constants.MAXLENGTH)
                return Constants.MESSAGE_LENGTH_ERROR;

            // Fixed header
            //      Publish (3.3)
            buffer[index++] = 0x30;
            
            // Encode the fixed header remaining length
            tmp = remainingLength;  
            do
            {
                digit = tmp % 128;
                tmp = tmp / 128;
                if (tmp > 0)
                {
                    digit = digit | 0x80;
                }
                buffer[index++] = (byte)digit;
            } while (tmp > 0);
            // End of fixed header 

            // Variable header (topic)
            buffer[index++] = (byte)(topic.Length / 255);  // Length MSB
            buffer[index++] = (byte)(topic.Length % 255);  // Length LSB

            // Topic
            for (var i = 0; i < topic.Length; i++)
            {
                buffer[index++] = (byte)topic[i]; 
            }

            // Message (Length is accounted for in the fixed header)
            for (var i = 0; i < message.Length; i++)
            {
                buffer[index++] = (byte)message[i];
            }
            return mySocket.Send(buffer, buffer.Length, 0);
        }

        // Disconnect from broker (3.14)
        public static int DisconnectMQTT(Socket mySocket)
        {
            byte[] buffer = null;
            buffer = new byte[2];
            buffer[0] = 0xe0;
            buffer[1] = 0x00;
            return mySocket.Send(buffer, buffer.Length, 0);
        }
    }
}
