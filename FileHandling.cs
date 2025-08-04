using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using System.Globalization;

namespace RiftRumbleStats
{
    public partial class FileHandling
    {
        public class PlayerData
        {
			public string fID { get; set; }
			public string gameLength { get; set; } // {"gameLength":milliseconds,
			public string playerName { get; set; }          //  \"RIOT_ID_GAME_NAME\":\"name\"
            public string playerChampion { get; set; }      // \"SKIN\":\"champ\",
            public string playerKillCount { get; set; }     // \"CHAMPIONS_KILLED\":\"count\"
            public string playerDeathCount { get; set; }    // \"NUM_DEATHS\":\"count\",
            public string playerAssistCount { get; set; }   // \"ASSISTS\":\"count\",
            public string playerTeam { get; set; }          //\"TEAM\":\"100 (red) 200 (blue)\",
            public string playerWinLose { get; set; }        // \"WIN\":\"Win/Fail\"  
            public string playerCreepScore { get; set; }    //  \"MINIONS_KILLED\":\"54\"
            public string playerGoldIncome { get; set; }    // \"GOLD_EARNED\":\"count\"
        }

        public static int CheckFileExtDiscord(SocketUserMessage msg)
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

        public static bool ExportCSVData(string csvPath, List<PlayerData> data)
        {
            if (false)
                return false;

            using (var writer = new StreamWriter(csvPath))
            using (var csvWrite = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csvWrite.WriteRecords(data);
                csvWrite.Flush();
            }

            return true;
        }
		public static bool LoadReplayFile(string replayPath, string csvPath)
        {
			// go through all of the files inside the directory, make sure they're not executables, make sure they're valid, add them to a list of valid files, then parse them
			if (!File.Exists(replayPath))
				return false;

            // Also make sure the output csv file exists..
            string gameID = "blahblah"; // get this once and then just copy it to the other entries..

			// need another place to put them once they're properly dealt with?
            using (var fs = new FileStream(replayPath, FileMode.Open, FileAccess.Read))
            {
                // make sure it's not an executable
                var reader = new BinaryReader(fs);
                // Check 'MZ' signature
                if (reader.ReadUInt16() != 0x5A4D) // 'MZ'
                {
                    // Read the PE header offset
                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peHeaderOffset = reader.ReadInt32();

                    // Check 'PE\0\0' signature
                    fs.Seek(peHeaderOffset, SeekOrigin.Begin);
                    uint peSignature = reader.ReadUInt32();
                    if (peSignature == 0x00004550) // 'PE\0\0'
                    {
                        Console.WriteLine(replayPath + " is an executable.");
                        // probably should move this to a folder for bad/errored files
                        return false;
                    }
                }

				// once it's valid, set up the list for the file
				var playerData = new List<PlayerData>();

				foreach (var player in playerData)
				{
					player.fID = gameID;
				}

				return ExportCSVData(csvPath, playerData);

			}
		}
    }
}
