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
using System.Text;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Socket = System.Net.Sockets.Socket;

namespace Netduino_MQTT_Client_Library
{
    static class Constants
    {

        // These have been scaled down to the hardware
        // Maximum values are commented out - you can 
        // adjust, but keep in mind the limits of the hardware

        public const int MQTTPROTOCOLVERSION = 3;
        //public const int MAXLENGTH = 268435455; // 256MB
        public const int MAXLENGTH = 10240; // 10K
        public const int MAX_CLIENTID = 23;
        public const int MIN_CLIENTID = 1;
        public const int MAX_KEEPALIVE = 65535;
        public const int MIN_KEEPALIVE = 0;
        //public const int MAX_USERNAME = 65535;
        //public const int MAX_PASSWORD = 65535;
        public const int MAX_USERNAME = 12;
        public const int MAX_PASSWORD = 12;
        //public const int MAX_TOPIC_LENGTH = 32767;
        public const int MAX_TOPIC_LENGTH = 256;
        public const int MIN_TOPIC_LENGTH = 1;
        public const int MAX_MESSAGEID = 65535;

        // Error Codes
        public const int CLIENTID_LENGTH_ERROR = 1;
        public const int KEEPALIVE_LENGTH_ERROR = 1;
        public const int MESSAGE_LENGTH_ERROR = 1;
        public const int TOPIC_LENGTH_ERROR = 1;
        public const int TOPIC_WILDCARD_ERROR = 1;
        public const int USERNAME_LENGTH_ERROR = 1;
        public const int PASSWORD_LENGTH_ERROR = 1;
        public const int CONNECTION_ERROR = 1;
        public const int ERROR = 1;
        public const int SUCCESS = 0;

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
        public const byte MQTT_CONNECT_TYPE = 0x10;
        public const byte MQTT_CONNACK_TYPE = 0x20;
        public const byte MQTT_PUBLISH_TYPE = 0x30;
        public const byte MQTT_PING_REQ_TYPE = 0xc0;
        public const byte MQTT_PING_RESP_TYPE = 0xd0;
        public const byte MQTT_DISCONNECT_TYPE = 0xe0;
        public const byte MQTT_SUBSCRIBE_TYPE = 0x82;
        public const byte MQTT_UNSUBSCRIBE_TYPE = 0xa2;

        // Flags
        public const int CLEAN_SESSION_FLAG = 0x02;
        public const int USING_USERNAME_FLAG = 0x80;
        public const int USING_PASSWORD_FLAG = 0x40;
        public const int CONTINUATION_BIT = 0x80;
    }

    public static class NetduinoMQTT
    {
        // Setup our random number generator
        private static Random rand = new Random((int)(Utility.GetMachineTime().Ticks & 0xffffffff));

        //***************************************************************
        //  Send Messages 
        //***************************************************************

        // Connect to the MQTT Server
        public static int ConnectMQTT(Socket mySocket, String clientID, int keepAlive = 20, bool cleanSession = true, String username = "", String password = "")
        {

            int index = 0;
            int tmp = 0;
            int remainingLength = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payload = 0;
            int returnCode = 0;
            bool usingUsername = false;
            bool usingPassword = false;
            byte connectFlags = 0x00;
            byte[] buffer = null;
            byte[] inputBuffer = new byte[1];
            byte firstByte = 0x00;

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
            // Add remaining length
            index = doRemainingLength(remainingLength, index, buffer);
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
            if (cleanSession)
                connectFlags |= (byte)Constants.CLEAN_SESSION_FLAG;
            if (usingUsername)
                connectFlags |= (byte)Constants.USING_USERNAME_FLAG;
            if (usingPassword)
                connectFlags |= (byte)Constants.USING_PASSWORD_FLAG;

            // Set the connect flags
            buffer[index++] = connectFlags;

            // Keep alive (defaulted to 20 seconds above)
            buffer[index++] = (byte)(keepAlive / 256); // Keep Alive MSB
            buffer[index++] = (byte)(keepAlive % 256); // Keep Alive LSB

            // ClientID
            buffer[index++] = (byte)(utf8ClientID.Length / 256); // Length MSB
            buffer[index++] = (byte)(utf8ClientID.Length % 256); // Length LSB
            for (var i = 0; i < utf8ClientID.Length; i++)
            {
                buffer[index++] = utf8ClientID[i];
            }

            // Username
            if (usingUsername)
            {
                buffer[index++] = (byte)(utf8Username.Length / 256); // Length MSB
                buffer[index++] = (byte)(utf8Username.Length % 256); // Length LSB

                for (var i = 0; i < utf8Username.Length; i++)
                {
                    buffer[index++] = utf8Username[i];
                }
            }

            // Password
            if (usingPassword)
            {
                buffer[index++] = (byte)(utf8Password.Length / 256); // Length MSB
                buffer[index++] = (byte)(utf8Password.Length % 256); // Length LSB

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
            returnCode = mySocket.Receive(inputBuffer, 0);

            if (returnCode < 1)
                return Constants.CONNECTION_ERROR;

            firstByte = inputBuffer[0];

            // If this is the CONNACK - pass it to the CONNACK handler
            if (((int)firstByte & Constants.MQTT_CONNACK_TYPE) > 0)
            {
                returnCode = handleCONNACK(mySocket, firstByte);
                if (returnCode > 0)
                {
                    return Constants.ERROR;
                }
            }
            return Constants.SUCCESS;
        }

        // Publish a message to a broker (3.3)
        public static int PublishMQTT(Socket mySocket, String topic, String message)
        {
            int index = 0;
            int tmp = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payload = 0;
            int remainingLength = 0;
            int returnCode = 0;
            byte[] buffer = null;

            // Setup a UTF8 encoder
            UTF8Encoding encoder = new UTF8Encoding();

            // Encode the topic
            byte[] utf8Topic = Encoding.UTF8.GetBytes(topic);

            // Some error checking
            // Topic contains wildcards
            if ((topic.IndexOf('#') != -1) || (topic.IndexOf('+') != -1))
                return Constants.TOPIC_WILDCARD_ERROR;

            // Topic is too long or short
            if ((utf8Topic.Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topic.Length < Constants.MIN_TOPIC_LENGTH))
                return Constants.TOPIC_LENGTH_ERROR;

            // Calculate the size of the var header
            varHeader += 2; // Topic Name Length (MSB, LSB)
            varHeader += utf8Topic.Length; // Length of the topic

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
            // Add remaining length
            index = doRemainingLength(remainingLength, index, buffer);
            // End Fixed Header

            // Start of Variable header
            // Length of topic name
            buffer[index++] = (byte)(utf8Topic.Length / 256); // Length MSB
            buffer[index++] = (byte)(utf8Topic.Length % 256); // Length LSB
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

            returnCode = mySocket.Send(buffer, buffer.Length, 0);

            if (returnCode < buffer.Length)
                return Constants.CONNECTION_ERROR;

            return Constants.SUCCESS;
        }

        // Disconnect from broker (3.14)
        public static int DisconnectMQTT(Socket mySocket)
        {
            byte[] buffer = null;
            int returnCode = 0;
            buffer = new byte[2];
            buffer[0] = Constants.MQTT_DISCONNECT_TYPE;
            buffer[1] = 0x00;
            returnCode = mySocket.Send(buffer, buffer.Length, 0);

            if (returnCode < buffer.Length)
                return Constants.CONNECTION_ERROR;

            return Constants.SUCCESS; ;
        }

        // Subscribe to a topic 
        public static int SubscribeMQTT(Socket mySocket, String[] topic, int[] QoS, int topics)
        {
            int index = 0;
            int index2 = 0;
            int messageIndex = 0;
            int messageID = 0;
            int tmp = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payloadLength = 0;
            int remainingLength = 0;
            int returnCode = 0;
            byte[] buffer = null;
            byte[][] utf8Topics = null;
            
            UTF8Encoding encoder = new UTF8Encoding();

            utf8Topics = new byte[topics][];

            while (index < topics)
            {
                utf8Topics[index] = new byte[Encoding.UTF8.GetBytes(topic[index]).Length];
                utf8Topics[index] = Encoding.UTF8.GetBytes(topic[index]);
                if ((utf8Topics[index].Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topics[index].Length < Constants.MIN_TOPIC_LENGTH))
                {
                    return Constants.TOPIC_LENGTH_ERROR;
                }
                else
                {
                    payloadLength += 2; // Size (LSB + MSB)
                    payloadLength += utf8Topics[index].Length;  // Length of topic
                    payloadLength++; // QoS Requested
                    index++;
                }
            }

            // Calculate the size of the fixed header
            fixedHeader++; // byte 1

            // Calculate the size of the var header
            varHeader += 2; // Message ID is 2 bytes

            // Calculate the remaining size
            remainingLength = varHeader + payloadLength;

            // Check that remaining encoded length will fit into 4 encoded bytes
            if (remainingLength > Constants.MAXLENGTH)
                return Constants.MESSAGE_LENGTH_ERROR;

            // Add space for each byte we need in the fixed header to store the length
            tmp = remainingLength;
            while (tmp > 0)
            {
                fixedHeader++;
                tmp = tmp / 128;
            };

            // Build buffer for message
            buffer = new byte[fixedHeader + varHeader + payloadLength];

            // Start of Fixed header
            // Publish (3.3)
            buffer[messageIndex++] = Constants.MQTT_SUBSCRIBE_TYPE;

            // Add remaining length
            messageIndex = doRemainingLength(remainingLength, messageIndex, buffer);
            // End Fixed Header

            // Start of Variable header
            // Message ID
            messageID = rand.Next(Constants.MAX_MESSAGEID);
            Debug.Print("SUBSCRIBE: Message ID: " + messageID);
            buffer[messageIndex++] = (byte)(messageID / 256); // Length MSB
            buffer[messageIndex++] = (byte)(messageID % 256); // Length LSB
            // End of variable header

            // Start of Payload
            index = 0;
            while (index < topics)
            {
                // Length of Topic
                buffer[messageIndex++] = (byte)(utf8Topics[index].Length / 256); // Length MSB 
                buffer[messageIndex++] = (byte)(utf8Topics[index].Length % 256); // Length LSB 
                
                index2 = 0;
                while (index2 < utf8Topics[index].Length)
                {
                    buffer[messageIndex++] = utf8Topics[index][index2];
                    index2++;
                }
                buffer[messageIndex++] = (byte)(QoS[index]);
                index++;
            }
            // End of Payload

            returnCode = mySocket.Send(buffer, buffer.Length, 0);

            if (returnCode < buffer.Length)
                return Constants.CONNECTION_ERROR;

            return Constants.SUCCESS;
        }

        // Unsubscribe to a topic
        public static int UnsubscribeMQTT(Socket mySocket, String[] topic, int[] QoS, int topics)
        {
            int index = 0;
            int index2 = 0;
            int messageIndex = 0;
            int messageID = 0;
            int tmp = 0;
            int fixedHeader = 0;
            int varHeader = 0;
            int payloadLength = 0;
            int remainingLength = 0;
            int returnCode = 0;
            byte[] buffer = null;
            byte[][] utf8Topics = null;

            UTF8Encoding encoder = new UTF8Encoding();

            utf8Topics = new byte[topics][];

            while (index < topics)
            {
                utf8Topics[index] = new byte[Encoding.UTF8.GetBytes(topic[index]).Length];
                utf8Topics[index] = Encoding.UTF8.GetBytes(topic[index]);
                if ((utf8Topics[index].Length > Constants.MAX_TOPIC_LENGTH) || (utf8Topics[index].Length < Constants.MIN_TOPIC_LENGTH))
                {
                    return Constants.TOPIC_LENGTH_ERROR;
                }
                else
                {
                    payloadLength += 2; // Size (LSB + MSB)
                    payloadLength += utf8Topics[index].Length;  // Length of topic
                    index++;
                }
            }

            // Calculate the size of the fixed header
            fixedHeader++; // byte 1

            // Calculate the size of the var header
            varHeader += 2; // Message ID is 2 bytes

            // Calculate the remaining size
            remainingLength = varHeader + payloadLength;

            // Check that remaining encoded length will fit into 4 encoded bytes
            if (remainingLength > Constants.MAXLENGTH)
                return Constants.MESSAGE_LENGTH_ERROR;

            // Add space for each byte we need in the fixed header to store the length
            tmp = remainingLength;
            while (tmp > 0)
            {
                fixedHeader++;
                tmp = tmp / 128;
            };

            // Build buffer for message
            buffer = new byte[fixedHeader + varHeader + payloadLength];

            // Start of Fixed header
            // Publish (3.3)
            buffer[messageIndex++] = Constants.MQTT_UNSUBSCRIBE_TYPE;

            // Add remaining length - writes to buffer, so need to get the new index back
            messageIndex = doRemainingLength(remainingLength, messageIndex, buffer);
            // End Fixed Header

            // Start of Variable header
            // Message ID
            messageID = rand.Next(Constants.MAX_MESSAGEID);
            Debug.Print("UNSUBSCRIBE: Message ID: " + messageID);
            buffer[messageIndex++] = (byte)(messageID / 256); // Length MSB
            buffer[messageIndex++] = (byte)(messageID % 256); // Length LSB
            // End of variable header

            // Start of Payload
            index = 0;
            while (index < topics)
            {
                // Length of Topic
                buffer[messageIndex++] = (byte)(utf8Topics[index].Length / 256); // Length MSB 
                buffer[messageIndex++] = (byte)(utf8Topics[index].Length % 256); // Length LSB 

                index2 = 0;
                while (index2 < utf8Topics[index].Length)
                {
                    buffer[messageIndex++] = utf8Topics[index][index2];
                    index2++;
                }
                index++;
            }
            // End of Payload

            returnCode = mySocket.Send(buffer, buffer.Length, 0);

            if (returnCode < buffer.Length)
                return Constants.CONNECTION_ERROR;

            return Constants.SUCCESS;
        }

        // Respond to a PINGRESP
        public static int sendPINGRESP(Socket mySocket)
        {
            int index = 0;
            int returnCode = 0;
            byte[] buffer = new byte[2];

            buffer[index++] = Constants.MQTT_PING_RESP_TYPE;
            buffer[index++] = 0x00;

            // Send the ping
            returnCode = mySocket.Send(buffer, index, 0);
            // The return code should equal our buffer length
            if (returnCode != buffer.Length)
            {
                return Constants.CONNECTION_ERROR;
            }
            return Constants.SUCCESS;
        }

        // Ping the MQTT broker - used to extend keep alive
        public static int PingMQTT(Socket mySocket)
        {

            int index = 0;
            int returnCode = 0;
            byte[] buffer = new byte[2]; 
 
            buffer[index++] = Constants.MQTT_PING_REQ_TYPE;
            buffer[index++] = 0x00;

            // Send the ping
            returnCode = mySocket.Send(buffer, index, 0);
            // The return code should equal our buffer length
            if (returnCode != buffer.Length)
            {
                return Constants.CONNECTION_ERROR;
            }
            return Constants.SUCCESS;
        }

        //***************************************************************
        //  Utility Functions
        //***************************************************************

        // Append the remaining length field for the fixed header
        public static int doRemainingLength(int remainingLength, int index, byte[] buffer)
        {
            int digit = 0;
            do
            {
                digit = remainingLength % 128;
                remainingLength /= 128;
                if (remainingLength > 0)
                {
                    digit = digit | Constants.CONTINUATION_BIT;
                }
                buffer[index++] = (byte)digit;
            } while (remainingLength > 0);
            return index;
        }


        // Extract the remaining length field from the fixed header
        public static int undoRemainingLength(Socket mySocket)
        {
            int multiplier = 1;
            int count = 0;
            int digit = 0;
            int remainingLength = 0;
            byte[] nextByte = new byte[1];
            do
            {
                if (mySocket.Receive(nextByte, 0) == 1)
                {
                    digit = (byte)nextByte[0];
                    remainingLength += ((digit & 0x7F) * multiplier);
                    multiplier *= 128;
                }
                count++;
            } while (((digit & 0x80) != 0) && count <4);
            return remainingLength;
        }

        //***************************************************************
        //  Main Listener loop
        //***************************************************************

        // Listen for data on the socket - call appropriate handlers based on first byte
        public static int listen(Socket mySocket)
        {
            int returnCode = 0;
            byte first = 0x00;
            byte[] buffer = new byte[1];
            while (true)
            {
                returnCode = mySocket.Receive(buffer, 0);
                if (returnCode > 0)
                {
                    Debug.Print("Data Received");
                    first = buffer[0];
                    switch (first >> 4)
                    {
                        case 0:  // Reserved
                            Debug.Print("First Reserved Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 1:  // Connect (Broker Only)
                            Debug.Print("CONNECT Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 2:  // CONNACK
                            Debug.Print("CONNACK Message received"); 
                            returnCode = handleCONNACK(mySocket, first);
                            break;
                        case 3:  // PUBLISH
                            Debug.Print("PUBLISH Message received"); 
                            returnCode = handlePUBLISH(mySocket, first);
                            break;
                        case 4:  // PUBACK (QoS > 0 - did it anyway)
                            Debug.Print("PUBACK Message received"); 
                            returnCode = handlePUBACK(mySocket, first);
                            break;
                        case 5:  // PUBREC (QoS 2)
                            Debug.Print("PUBREC Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 6:  // PUBREL (QoS 2)
                            Debug.Print("PUBREL Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 7:  // PUBCOMP (QoS 2)
                            Debug.Print("PUBCOMP Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 8:  // SUBSCRIBE (Broker only)
                            Debug.Print("SUBSCRIBE Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 9:  // SUBACK 
                            Debug.Print("SUBACK Message received"); 
                            returnCode = handleSUBACK(mySocket, first);
                            break;
                        case 10:  // UNSUBSCRIBE (Broker Only)
                            Debug.Print("UNSUBSCRIBE Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 11:  // UNSUBACK
                            Debug.Print("UNSUBACK Message received"); 
                            returnCode = handleUNSUBACK(mySocket, first);
                            break;
                        case 12:  // PINGREQ (Technically a Broker Deal - but we're doing it anyway)
                            Debug.Print("PINGREQ Message received");
                            returnCode = handlePINGREQ(mySocket, first);
                            break;
                        case 13:  // PINGRESP
                            Debug.Print("PINGRESP Message received"); 
                            returnCode = handlePINGRESP(mySocket, first);
                            break;
                        case 14:  // DISCONNECT (Broker Only)
                            Debug.Print("DISCONNECT Message received");
                            returnCode = Constants.ERROR;
                            break;
                        case 15:  // Reserved
                            Debug.Print("Last Reserved Message received");
                            returnCode = Constants.ERROR;
                            break;
                        default:  // Default action
                            Debug.Print("Unknown Message received");  // Should never get here
                            returnCode = Constants.ERROR;
                            break;
                    }
                    if (returnCode != Constants.SUCCESS)
                    {
                        Debug.Print("An error occurred in message processing");
                    }
                }
            }
        }

        //***************************************************************
        //  Message handlers (received)
        //***************************************************************

        public static int handleSUBACK(Socket mySocket, byte firstByte)
        {
            int remainingLength = 0;
            int messageID = 0;
            int index = 0;
            int QoSIndex = 0;
            int[] QoS = null;
            byte[] buffer = null;
            remainingLength = undoRemainingLength(mySocket);
            buffer = new byte[remainingLength];
            if ((mySocket.Receive(buffer, 0) != remainingLength) || remainingLength < 3)
                return Constants.ERROR;
            messageID += buffer[index++] * 256;
            messageID += buffer[index++];
            Debug.Print("SUBACK: Message ID: " + messageID);
            do 
            {
                QoS = new int[remainingLength - 2];
                QoS[QoSIndex++] = buffer[index++];
                Debug.Print("SUBACK: QoS Granted: " + QoS[QoSIndex-1]);
            } while(index < remainingLength);
            return Constants.SUCCESS;
        }

        // Messages from the broker come back to us as publish messages
        public static int handlePUBLISH(Socket mySocket, byte firstByte)
        {
            int remainingLength = 0;
            int messageID = 0;
            int topicLength = 0;
            int topicIndex = 0;
            int payloadIndex = 0;
            int index = 0;
            byte[] buffer = null;
            byte[] topic = null;
            byte[] payload = null;
            int QoS = 0x00;
            String topicString = null; 
            String payloadString = null;

            remainingLength = undoRemainingLength(mySocket);
            buffer = new byte[remainingLength];
            if ((mySocket.Receive(buffer, 0) != remainingLength) || remainingLength < 5)
                return Constants.ERROR;
            topicLength += buffer[index++] * 256;
            topicLength += buffer[index++];
            topic = new byte[topicLength];
            while (topicIndex < topicLength)
            {
                topic[topicIndex++] = buffer[index++];
            }
            QoS = firstByte & 0x06;
            if (QoS > 0)
            {
                messageID += buffer[index++] * 256;
                messageID += buffer[index++];
                Debug.Print("PUBLISH: Message ID: " + messageID);
            }
            topicString = new String(Encoding.UTF8.GetChars(topic));
            Debug.Print("PUBLISH: Topic: " + topicString);
            payload = new byte[remainingLength - index];
            while (index < remainingLength)
            {
                payload[payloadIndex++] = buffer[index++];
            }

            Debug.Print("PUBLISH: Payload Length: " + payload.Length);

            // This doesn't work if the payload isn't UTF8
            payloadString = new String(Encoding.UTF8.GetChars(payload));
            Debug.Print("PUBLISH: Payload: " + payloadString);

            return Constants.SUCCESS;
        }

        public static int handleUNSUBACK(Socket mySocket, byte firstByte)
        {
            int returnCode = 0;
            int messageID = 0;
            byte[] buffer = new byte[3];
            returnCode = mySocket.Receive(buffer, 0);
            if ((buffer[0] != 2) || (returnCode != 3))
                return Constants.ERROR;
            messageID += buffer[1] * 256;
            messageID += buffer[2];
            Debug.Print("UNSUBACK: Message ID: " + messageID);
            return Constants.SUCCESS;
        }

        // Ping response - this should be a total of 2 bytes - that's pretty much 
        // all I'm looking for.
        public static int handlePINGRESP(Socket mySocket, byte firstByte)
        {
            int returnCode = 0;
            Debug.Print("Ping Response Received");
            byte[] buffer = new byte[1];
            returnCode = mySocket.Receive(buffer, 0);
            if ((buffer[0] != 0) || (returnCode != 1))
            {
                return Constants.ERROR;
            }
            return Constants.SUCCESS;
        }

        // Ping Request 
        public static int handlePINGREQ(Socket mySocket, byte firstByte)
        {
            int returnCode = 0;
            byte[] buffer = new byte[1];
            returnCode = mySocket.Receive(buffer, 0);
            if((returnCode != 1) || (buffer[0] != 0))
                return Constants.ERROR;
            returnCode = sendPINGRESP(mySocket);
            if (returnCode != 0)
                return Constants.ERROR;
            return Constants.SUCCESS;
        }

        // Connect acknowledgement - returns 3 more bytes, byte 3 
        // should be 0 for success
        public static int handleCONNACK(Socket mySocket, byte firstByte)
        {
            int returnCode = 0;
            byte[] buffer = new byte[3];
            returnCode = mySocket.Receive(buffer, 0);
            if ((buffer[0] != 2) || (buffer[2] > 0) || (returnCode != 3))
                return Constants.ERROR;
            return Constants.SUCCESS;
        }

        // We're not doing QoS 1 yet, so this is just here for flushing 
        // and to notice if we are getting this message for some reason
        public static int handlePUBACK(Socket mySocket, byte firstByte)
        {
            int returnCode = 0;
            int messageID = 0;
            byte[] buffer = new byte[3];
            returnCode = mySocket.Receive(buffer, 0);
            if ((buffer[0] != 2) || (returnCode != 3))
                return Constants.ERROR;
            messageID += buffer[1] * 256;
            messageID += buffer[2];
            Debug.Print("PUBACK: Message ID: " + messageID);
            return Constants.SUCCESS;
        }
    }
}


