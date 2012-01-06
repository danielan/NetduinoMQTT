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

        // Error Codes
        public const int CLIENTID_LENGTH_ERROR = -1;
        public const int KEEPALIVE_LENGTH_ERROR = -2;
        public const int MESSAGE_LENGTH_ERROR = -3;
        public const int TOPIC_LENGTH_ERROR = -4;
        public const int TOPIC_WILDCARD_ERROR = -5;
        public const int USERNAME_LENGTH_ERROR = -6;
        public const int PASSWORD_LENGTH_ERROR = -7;

        // Message types
        public const int MQTT_CONNECT_TYPE = 0x10;
        public const int MQTT_PUBLISH_TYPE = 0x30;
        public const int MQTT_DISCONNECT_TYPE = 0xe0;

        // Flags
        public const int CLEAN_SESSION_FLAG = 0x02;
        public const int USING_USERNAME_FLAG = 0x80;
        public const int USING_PASSWORD_FLAG = 0x40;
        public const int CONTINUATION_BIT = 0x80;
    }

    public static class NetduinoMQTT
    {

        public static int ConnectMQTT(Socket mySocket, String clientID, int keepAlive = 20, bool cleanSession = true, String username = "", String password = "")
        {

            int index = 0;
            int tmp = 0;
            int digit = 0;
            int remainingLength = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payload = 0;
            bool usingUsername = false;
            bool usingPassword = false;
            byte connectFlags = 0x00;
            byte[] buffer = null;

            // Some Error Checking
            // ClientID improperly sized
            if ((clientID.Length > 23) || (clientID.Length < 1))
                return Constants.CLIENTID_LENGTH_ERROR;
            // KeepAlive out of bounds
            if ((keepAlive > 65535) || (keepAlive < 0))
                return Constants.KEEPALIVE_LENGTH_ERROR;
            // Username too long
            if (username.Length > 65535)
                return Constants.USERNAME_LENGTH_ERROR;
            // Password too long
            if (password.Length > 65535)
                return Constants.PASSWORD_LENGTH_ERROR;

            // Check features being used
            if (!username.Equals(""))
                usingUsername = true;
            if (!password.Equals(""))
                usingPassword = true;

            // Calculate the size of the var header
            varHeader += 2;                    // Protocol Name Length
            varHeader += 6;                    // Protocol Name
            varHeader++;                       // Protocol version
            varHeader++;                       // Connect Flags
            varHeader += 2;                    // Keep Alive 

            // Calculate the size of the fixed header
            fixedHeader++;                      // byte 1
              
            // Calculate the payload
            payload = clientID.Length + 2;
            if (usingUsername)
            {
                payload += username.Length + 2;
            }
            if (usingPassword)
            {
                payload += password.Length + 2;
            }

            // Calculate the remaining size
            remainingLength = varHeader + payload;

            // Check that remaining length will fit into 4 encoded bytes
            if (remainingLength > Constants.MAXLENGTH)
                return Constants.MESSAGE_LENGTH_ERROR;

            tmp = remainingLength; 

            // Add space for each byte we need in the fixed header to store the length
            while (tmp > 0)
            {
                fixedHeader++;
                tmp = tmp / 128;
            };
            // End of Fixed Header

            // Build buffer for message
            buffer = new byte[fixedHeader+varHeader+payload]; 
            
            // Fixed Header (2.1)
            buffer[index++] = Constants.MQTT_CONNECT_TYPE;    

            // Encode the fixed header remaining length
            tmp = remainingLength;
            do
            {
                digit = tmp % 128;
                tmp = tmp / 128;
                if (tmp > 0)
                {
                    digit = digit | Constants.CONTINUATION_BIT; 
                }
                buffer[index++] = (byte)digit;
            } while (tmp > 0);
            // End Fixed Header
            
            // Connect (3.1) 
            // Protocol Name
            buffer[index++] = 0;                // String (MQIsdp) Length MSB - always 6 so, zeroed
            buffer[index++] = 6;                // Length LSB
            buffer[index++] = (byte)'M';        // M
            buffer[index++] = (byte)'Q';        // Q
            buffer[index++] = (byte)'I';        // I
            buffer[index++] = (byte)'s';        // s
            buffer[index++] = (byte)'d';        // d
            buffer[index++] = (byte)'p';        // p
            
            // Protocol Version
            buffer[index++] = Constants.MQTTPROTOCOLVERSION;
            
            // Connect Flags 
            // TODO: re-read the spec to figure out what the "will" stuff is about
            if (cleanSession)
                connectFlags |= (byte)Constants.CLEAN_SESSION_FLAG;
            if (usingUsername)
                connectFlags |= (byte)Constants.USING_USERNAME_FLAG;
            if (usingPassword)
                connectFlags |= (byte)Constants.USING_PASSWORD_FLAG;

            // Set the connect flags
            buffer[index++] = connectFlags;
            
            // Keep alive (defaulted to 20 seconds above)
            buffer[index++] = (byte)(keepAlive / 255);   // Keep Alive MSB
            buffer[index++] = (byte)(keepAlive % 255);   // Keep Alive LSB
            
            // ClientID 
            buffer[index++] = (byte)(clientID.Length / 255);     // Length MSB
            buffer[index++] = (byte)(clientID.Length % 255);     // Length LSB
            for (var i = 0; i < clientID.Length; i++) 
            {
                buffer[index++] = (byte)clientID[i]; 
            }

            // Username
            if (usingUsername)
            {
                buffer[index++] = (byte)(username.Length / 255);     // Length MSB
                buffer[index++] = (byte)(username.Length % 255);     // Length LSB

                for (var i = 0; i < username.Length; i++)
                {
                    buffer[index++] = (byte)username[i];
                }
            }

            // Password
            if (usingPassword)
            {
                buffer[index++] = (byte)(password.Length / 255);     // Length MSB
                buffer[index++] = (byte)(password.Length % 255);     // Length LSB

                for (var i = 0; i < password.Length; i++)
                {
                    buffer[index++] = (byte)password[i];
                }
            }
            return mySocket.Send(buffer, index, 0); ;
        }

        // Publish a message to a broker (3.3)
        public static int PublishMQTT(Socket mySocket, String topic, String message)
        {

            int index = 0;
            int digit = 0;
            int tmp = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payload = 0;
            int remainingLength = 0;
            byte[] buffer = null;

            // Some error checking
            // Topic is too long or short
            if ((topic.Length > 32767)||(topic.Length < 1))
                return Constants.TOPIC_LENGTH_ERROR;
            // Topic contains wildcards
            if ((topic.IndexOf('#')!=-1)||(topic.IndexOf('+')!=-1))
                return Constants.TOPIC_WILDCARD_ERROR;

            // Calculate the size of the var header
            varHeader += 2;                    // Topic Name Length
            varHeader += topic.Length;         // Topic Name

            // Calculate the size of the fixed header
            fixedHeader++;                      // byte 1

            // Calculate the payload
            payload = message.Length;
            
            // Calculate the remaining size
            remainingLength = varHeader + payload;

            // Check that remaining length will fit into 4 encoded bytes
            if (remainingLength > Constants.MAXLENGTH)
                return Constants.MESSAGE_LENGTH_ERROR;
            
            // Add space for each byte we need in the fixed header to store the length
            tmp = remainingLength;
            while (tmp > 0)
            {
                fixedHeader++;
                tmp = tmp / 128;
            };
            // End of Fixed Header

            // Build buffer for message
            buffer = new byte[fixedHeader + varHeader + payload]; 
            
            // Start of Fixed header
            //      Publish (3.3)
            buffer[index++] = Constants.MQTT_PUBLISH_TYPE;
            
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

            // Start of Variable header 
            // Length of topic name
            buffer[index++] = (byte)(topic.Length / 255);  // Length MSB
            buffer[index++] = (byte)(topic.Length % 255);  // Length LSB
            // Topic
            for (var i = 0; i < topic.Length; i++)
            {
                buffer[index++] = (byte)topic[i]; 
            }
            // End of variable header

            // Start of Payload
            // Message (Length is accounted for in the fixed header)
            for (var i = 0; i < message.Length; i++)
            {
                buffer[index++] = (byte)message[i];
            }
            // End of Payload

            return mySocket.Send(buffer, buffer.Length, 0);
        }

        // Disconnect from broker (3.14)
        public static int DisconnectMQTT(Socket mySocket)
        {
            byte[] buffer = null;
            buffer = new byte[2];
            buffer[0] = Constants.MQTT_DISCONNECT_TYPE;
            buffer[1] = 0x00;
            return mySocket.Send(buffer, buffer.Length, 0);
        }
    }
}

