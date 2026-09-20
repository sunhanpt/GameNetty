using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ET
{
	public static class NetworkHelper
	{
		public static string[] GetAddressIPs()
		{
			List<string> list = new List<string>();
			foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
			{
				if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
				{
					continue;
				}
				foreach (UnicastIPAddressInformation add in networkInterface.GetIPProperties().UnicastAddresses)
				{
					list.Add(add.Address.ToString());
				}
			}
			return list.ToArray();
		}
		
		// 优先获取IPV4的地址
		public static IPAddress GetHostAddress(string hostName)
		{
			IPAddress[] ipAddresses = Dns.GetHostAddresses(hostName);
			IPAddress returnIpAddress = null;
			foreach (IPAddress ipAddress in ipAddresses)
			{
				returnIpAddress = ipAddress;
				if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					return ipAddress;
				}
			}
			return returnIpAddress;
		}
		
		public static string EnvOr(string name, string fallback)
		{
			string value = Environment.GetEnvironmentVariable(name);
			return string.IsNullOrWhiteSpace(value) ? fallback : value;
		}

		public static bool BindAny => Environment.GetEnvironmentVariable("ET_BIND_ANY") == "1";

		public static IPEndPoint ListenEndPoint(int port, IPEndPoint fallback)
		{
			if (BindAny)
			{
				return new IPEndPoint(IPAddress.Any, port);
			}
			return fallback;
		}

		public static string WebSocketPrefix(int port)
		{
			return $"http://*:{port}/";
		}

		public static IPEndPoint ToIPEndPoint(string host, int port)
		{
			return new IPEndPoint(IPAddress.Parse(host), port);
		}

		public static IPEndPoint ToIPEndPoint(string address)
		{
			int index = address.LastIndexOf(':');
			string host = address.Substring(0, index);
			string p = address.Substring(index + 1);
			int port = int.Parse(p);
			return ToIPEndPoint(host, port);
		}

		public static void SetSioUdpConnReset(Socket socket)
		{
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return;
			}

			const uint IOC_IN = 0x80000000;
			const uint IOC_VENDOR = 0x18000000;
			const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));

			socket.IOControl(SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
		}
	}
}
