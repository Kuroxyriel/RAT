using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace Virus {
	class Program {
    	private static IPEndPoint IPEP = IPEndPoint.Parse("127.0.0.1:42068");
		private static void Main(string[] args) {
			List<byte> bytes = new List<byte>() { 0x0, (byte)Message.Type.Ls };
			bytes.AddRange(Encoding.ASCII.GetBytes("."));
			bytes.AddRange(Shared.ACK);


			UdpClient udp = new UdpClient();
			udp.Send(bytes.ToArray(), IPEP);
		}
	}
}