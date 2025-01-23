using System.Text;
using System.Net.Sockets;

namespace Virus {
	class Shared {
		public static byte[] ACK = Encoding.ASCII.GetBytes("<|ACK|>");
		public static int ContainsACK(List<byte> arr) {
			for (int i = 0; i < arr.Count - ACK.Length +1; i++)
				if (arr.GetRange(i,ACK.Length).SequenceEqual(ACK))
					return i;
			return -1;
		}

		public static Message Read(Socket sock, ref byte[]? moarData, byte expectedID = 255) {
			byte[] id = new byte[1];
			List<byte> data = new List<byte>();
			if (moarData == null) sock.Receive(id);
			else {
				id[0] = moarData[0];
				data.AddRange(moarData.Skip(1));
				moarData = null;
			}

			byte[] buf = new byte[1024];
			int ind = -1;
			int read = 0;
			while ((ind = ContainsACK(data)) == -1 && (read = sock.Receive(buf)) > -1) 
				data.AddRange(buf.Take(read));
			if (ind+ACK.Length < data.Count)
				moarData = data.Skip(ind+ACK.Length).ToArray();

			if (Enum.IsDefined(typeof(Message.Type), id[0]) && (expectedID == 255 || id[0] == expectedID))
				return new Message((Message.Type)id[0], data.Take(ind));
			else {
				IOException ex;
				if (Enum.IsDefined(typeof(Message.Type), id[0]) && expectedID < 255) {
					ex = new IOException("Message ID differs from the expected one", 0);
					ex.Data["msgExpID"] = expectedID;
				} else
					ex = new IOException("Message ID is invalid", 1);
				ex.Data["msgID"] = id[0];
				ex.Data["msgData"] = data.ToArray();
				throw ex;
			}
		}

		public static string AttributeString(FileAttributes attr) {
			string att = string.Empty;
			if ((attr&FileAttributes.ReadOnly) == FileAttributes.ReadOnly) att += "R";
			else att += "-";

			if ((attr&FileAttributes.Hidden) == FileAttributes.Hidden) att += "H";
			else att += "-";

			if ((attr&FileAttributes.System) == FileAttributes.System) att += "S";
			else att += "-";

			if ((attr&FileAttributes.Compressed) == FileAttributes.Compressed) att += "C";
			else att += "-";

			if ((attr&FileAttributes.Encrypted) == FileAttributes.Encrypted) att += "E";
			else att += "-";

			return att;
		}

		private static readonly string[] SizeSuffixes = 
		                   { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		public static string SizeSuffix(long value) {
			if (value < 0) return "-" + SizeSuffix(-value); 
			if (value == 0) return string.Format("0 {1}", SizeSuffixes[0]);

			int mag = (int)Math.Log(value, 1024);
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));
			if (Math.Round(adjustedSize, 1) >= 1000) {
				mag += 1;
				adjustedSize /= 1024;
			}

			return string.Format("{"+(mag==0?"0":"0:n1")+"} {1}", adjustedSize, SizeSuffixes[mag]);
		}
	}
}