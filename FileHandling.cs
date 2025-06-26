using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RiftRumbleStats
{
    public partial class FileHandling
    {
      public static int CheckFileExt(SocketUserMessage msg)
      {
            if (msg.Attachments.Count > 0)
            {
                var attachment = msg.Attachments;
                var fileType = attachment.ElementAt(0).ContentType;
                // can we just assume if it has an attachment and it's null that it's a rofl? and then double check after so we dont have to do regex stuff on every message?
                if (fileType == null)
                {
                    var roflTrim = attachment.ElementAt(0).Url;
                    Match match = Regex.Match(roflTrim, @"\.rofl\b");
                    string fixString = match.ToString();
                    fixString = fixString.TrimStart('{');
                    fixString = fixString.TrimEnd('}');
                    if (fixString.Equals(".rofl"))
                        return 2;

                }
                return 1;
            }            
            return 0;
        }
        
        public static bool IsWindowsEXE(string path)
        {
            if (!File.Exists(path))
                return false;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var reader = new BinaryReader(fs);
                // Check 'MZ' signature
                if (reader.ReadUInt16() != 0x5A4D) // 'MZ'
                    return false;

                // Read the PE header offset
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peHeaderOffset = reader.ReadInt32();

                // Check 'PE\0\0' signature
                fs.Seek(peHeaderOffset, SeekOrigin.Begin);
                uint peSignature = reader.ReadUInt32();
                return peSignature == 0x00004550; // 'PE\0\0'
            }
        }
    }
}
