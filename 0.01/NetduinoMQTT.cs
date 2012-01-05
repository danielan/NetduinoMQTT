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
		public const int MQTTVERSION = 3;  
	}  
  
	public static class MQTTClient  
	{  
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
  
		public static void ConnectMQTT(Socket mySocket, String clientID)  
		{  
  
			int index=0;  
			byte[] buffer = null;  
			buffer = new byte[16 + clientID.Length];  
			// Fixed Header (2.1)  
			buffer[index++] = 0x10; // Fixed header flags  
			buffer[index++] = (byte)(14 + clientID.Length); // Remaining Length  
			// End Fixed Header  
			// Connect (3.1)   
			// Protocol Name  
			buffer[index++] = 0;       // String (MQIsdp) Length MSB  
			buffer[index++] = 6;       // Length LSB  
			buffer[index++] = (byte)'M';  // M  
			buffer[index++] = (byte)'Q';  // Q  
			buffer[index++] = (byte)'I';  // I  
			buffer[index++] = (byte)'s';  // s  
			buffer[index++] = (byte)'d';  // d  
			buffer[index++] = (byte)'p';  // p  
			// Protocol Version  
			buffer[index++] = Constants.MQTTVERSION;  
			// Connect Flags  
			buffer[index++] = 0x02;  
			//Keep alive (20 seconds)  
			buffer[index++] = 0;     // Keep Alive MSB  
			buffer[index++] = 20;    // Keep Alive LSB  
			// ClientID  
			buffer[index++] = 0;                        // Length MSB  
			buffer[index++] = (byte)clientID.Length;    // Length LSB  
			for (var i = 0; i < clientID.Length; i++)   
			{  
				buffer[index++] = (byte)clientID[i];   
			}  
			mySocket.Send(buffer, index, 0);  
  
		}  
  
		public static void PublishMQTT(Socket mySocket, String topic, String message)  
		{  
  
			int index = 0;  
			byte[] buffer = null;  
			buffer = new byte[4 + topic.Length + message.Length];  
			// Fixed header  
			//      Publish (3.3) fixed header flags  
			buffer[index++] = 0x30;  
			//      Remaining Length (variable header + payload)  
			buffer[index++] = (byte)(2 + topic.Length + message.Length);  
			// End of fixed header   
			// Variable header (topic)  
			buffer[index++] = 0;                   // Length MSB  
			buffer[index++] = (byte)topic.Length;  // Length LSB  
			// End of variable header  
			for (var i = 0; i < topic.Length; i++)  
			{  
				buffer[index++] = (byte)topic[i];  
			}  
			// Message (Length is accounted for in the fixed header)  
			for (var i = 0; i < message.Length; i++)  
			{  
				buffer[index++] = (byte)message[i];  
			}  
			mySocket.Send(buffer, buffer.Length, 0);  
		}  
  
		public static void DisconnectMQTT(Socket mySocket)  
		{  
			byte[] buffer = null;  
			buffer = new byte[2];  
			buffer[0] = 0xe0;  
			buffer[1] = 0x00;  
			mySocket.Send(buffer, buffer.Length, 0);  
		}  
	}  
}  