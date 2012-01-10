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
        public const int MAX_CLIENTID = 23;
        public const int MIN_CLIENTID = 1;
        public const int MAX_KEEPALIVE = 65535;
        public const int MIN_KEEPALIVE = 0;
        public const int MAX_USERNAME = 65535;
        public const int MAX_PASSWORD = 65535;
        public const int MAX_TOPIC_LENGTH = 32767;
        public const int MIN_TOPIC_LENGTH = 1;

        // Error Codes
        public const int CLIENTID_LENGTH_ERROR = 1;
        public const int KEEPALIVE_LENGTH_ERROR = 1;
        public const int MESSAGE_LENGTH_ERROR = 1;
        public const int TOPIC_LENGTH_ERROR = 1;
        public const int TOPIC_WILDCARD_ERROR = 1;
        public const int USERNAME_LENGTH_ERROR = 1;
        public const int PASSWORD_LENGTH_ERROR = 1;
        public const int CONNECTION_ERROR = 1;

        public const int CONNECTION_OK = 0;

        public const int CONNACK_LENGTH = 4;
        public const int PINGRESP_LENGTH = 2;

        public const byte MQTT_CONN_OK = 0x00;  // Connection Accepted
        public const byte MQTT_CONN_BAD_PROTOCOL_VERSION = 0x01;  // Connection Refused: unacceptable protocol version
        public const byte MQTT_CONN_BAD_IDENTIFIER = 0x02;  // Connection Refused: identifier rejected
        public const byte MQTT_CONN_SERVER_UNAVAILABLE = 0x03;  // Connection Refused: server unavailable
        public const byte MQTT_CONN_BAD_AUTH = 0x04;  //  Connection Refused: bad user name or password
        public const byte MQTT_CONN_NOT_AUTH = 0x05;  //  Connection Refused: not authorized

        // Message types
        public const int MQTT_CONNECT_TYPE = 0x10;
        public const int MQTT_CONNACK_TYPE = 0x20;
        public const int MQTT_PUBLISH_TYPE = 0x30;
        public const int MQTT_PING_REQ_TYPE = 0xc0;
        public const int MQTT_PING_RESP_TYPE = 0xd0;
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
            int returnCode = 0;
            bool usingUsername = false;
            bool usingPassword = false;
            byte connectFlags = 0x00;
            byte[] buffer = null;

            UTF8Encoding encoder = new UTF8Encoding();

            byte[] utf8ClientID = Encoding.UTF8.GetBytes(clientID);
            byte[] utf8Username = Encoding.UTF8.GetBytes(username);
            byte[] utf8Password = Encoding.UTF8.GetBytes(password);

            // Some Error Checking
            // ClientID improperly sized
            if ((utf8ClientID.Length > Constants.MAX_CLIENTID) || (utf8ClientID.Length < Constants.MIN_CLIENTID))
                return Constants.CLIENTID_LENGTH_ERROR;
            // KeepAlive out of bounds
            if ((keepAlive > Constants.MAX_KEEPALIVE) || (keepAlive < Constants.MIN_KEEPALIVE))
                return Constants.KEEPALIVE_LENGTH_ERROR;
            // Username too long
            if (utf8Username.Length > Constants.MAX_USERNAME)
                return Constants.USERNAME_LENGTH_ERROR;
            // Password too long
            if (utf8Password.Length > Constants.MAX_PASSWORD)
                return Constants.PASSWORD_LENGTH_ERROR;

            // Check features being used
            if (!username.Equals(""))
                usingUsername = true;
            if (!password.Equals(""))
                usingPassword = true;

            // Calculate the size of the var header
            varHeader += 2; // Protocol Name Length
            varHeader += 6; // Protocol Name
            varHeader++; // Protocol version
            varHeader++; // Connect Flags
            varHeader += 2; // Keep Alive

            // Calculate the size of the fixed header
            fixedHeader++; // byte 1

            // Calculate the payload
            payload = utf8ClientID.Length + 2;
            if (usingUsername)
            {
                payload += utf8Username.Length + 2;
            }
            if (usingPassword)
            {
                payload += utf8Password.Length + 2;
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
            buffer = new byte[fixedHeader + varHeader + payload];

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
            buffer[index++] = 0; // String (MQIsdp) Length MSB - always 6 so, zeroed
            buffer[index++] = 6; // Length LSB
            buffer[index++] = (byte)'M'; // M
            buffer[index++] = (byte)'Q'; // Q
            buffer[index++] = (byte)'I'; // I
            buffer[index++] = (byte)'s'; // s
            buffer[index++] = (byte)'d'; // d
            buffer[index++] = (byte)'p'; // p

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
            buffer[index++] = (byte)(keepAlive / 255); // Keep Alive MSB
            buffer[index++] = (byte)(keepAlive % 255); // Keep Alive LSB

            // ClientID
            buffer[index++] = (byte)(utf8ClientID.Length / 255); // Length MSB
            buffer[index++] = (byte)(utf8ClientID.Length % 255); // Length LSB
            for (var i = 0; i < utf8ClientID.Length; i++)
            {
                buffer[index++] = utf8ClientID[i];
            }

            // Username
            if (usingUsername)
            {
                buffer[index++] = (byte)(utf8Username.Length / 255); // Length MSB
                buffer[index++] = (byte)(utf8Username.Length % 255); // Length LSB

                for (var i = 0; i < utf8Username.Length; i++)
                {
                    buffer[index++] = utf8Username[i];
                }
            }

            // Password
            if (usingPassword)
            {
                buffer[index++] = (byte)(utf8Password.Length / 255); // Length MSB
                buffer[index++] = (byte)(utf8Password.Length % 255); // Length LSB

                for (var i = 0; i < utf8Password.Length; i++)
                {
                    buffer[index++] = utf8Password[i];
                }
            }
            
            // Send the message
            returnCode = mySocket.Send(buffer, index, 0);
            
            // The return code should equal our buffer length
            if (returnCode != buffer.Length)
            {
                return Constants.CONNECTION_ERROR;
            }

            // Get the acknowledgement message
            returnCode = mySocket.Receive(buffer, 0);
            
            // The length should equal 4
            if (returnCode != Constants.CONNACK_LENGTH)
            {
                return Constants.CONNECTION_ERROR;
            }
            
            // This should be a message type 2 (CONNACK)
            if (buffer[0] == Constants.MQTT_CONNACK_TYPE)
            {
                // This is our return code from the server
                return buffer[3];
            }
            
            // If not zero = return the return code
            return Constants.CONNECTION_ERROR;
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

            UTF8Encoding encoder = new UTF8Encoding();

            byte[] utf8Topic = Encoding.UTF8.GetBytes(topic);

            // Some error checking
            
            // Topic contains wildcards
            if ((topic.IndexOf('#') != -1) || (topic.IndexOf('+') != -1))
                return Constants.TOPIC_WILDCARD_ERROR;
            
            // Topic is too long or short
            if ((utf8Topic.Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topic.Length < Constants.MIN_TOPIC_LENGTH))
                return Constants.TOPIC_LENGTH_ERROR;

            // Calculate the size of the var header
            varHeader += 2; // Topic Name Length
            varHeader += utf8Topic.Length; // Topic Name

            // Calculate the size of the fixed header
            fixedHeader++; // byte 1

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
            // Publish (3.3)
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
            buffer[index++] = (byte)(utf8Topic.Length / 255); // Length MSB
            buffer[index++] = (byte)(utf8Topic.Length % 255); // Length LSB
            // Topic
            for (var i = 0; i < utf8Topic.Length; i++)
            {
                buffer[index++] = utf8Topic[i];
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

        // Ping the MQTT broker - used to extend keep alive
        public static int PingMQTT(Socket mySocket)
        {

            int index = 0;
            int returnCode = 0;
            byte[] buffer = null;

            buffer = new byte[2];
            buffer[index++] = Constants.MQTT_PING_REQ_TYPE; 
            buffer[index++] = 0x00;

            // Send the ping
            returnCode = mySocket.Send(buffer, index, 0);

            // The return code should equal our buffer length
            if (returnCode != buffer.Length)
            {
                return Constants.CONNECTION_ERROR;
            }

            // Get the acknowledgement message
            returnCode = mySocket.Receive(buffer, 0);

            // The length should equal 2
            if (returnCode != Constants.PINGRESP_LENGTH)
            {
                return Constants.CONNECTION_ERROR;
            }

            // This should be a message type 2 (CONNACK)
            if (buffer[0] == Constants.MQTT_PING_RESP_TYPE)
            {
                return 0;
            }
            return Constants.CONNECTION_ERROR;
        }
    }
}


