//#define SHEETBUILDDEBUG

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RiftRumbleStats
{
	public partial class FileHandling
    {
		//private static readonly SemaphoreSlim fileLock = new SemaphoreSlim(1, 1);
		public class PlayerData
        {
			public string gameID {get; set;}
            public string gameLength { get; set; }
			public string RIOT_ID_GAME_NAME { get; set; }  // name
            public string SKIN { get; set; }      // champ
            public string CHAMPIONS_KILLED { get; set; }  //count
            public string NUM_DEATHS { get; set; }  // count
            public string ASSISTS { get; set; }   // count
            public string TEAM { get; set; }  //100 (red) 200 (blue)
            public string WIN { get; set; }  // Win/Fail  
            public string MINIONS_KILLED { get; set; }   // count
            public string GOLD_EARNED { get; set; }    // count
        }

		public class GameData
        {
            public long gameLength { get; set; } // {"gameLength":milliseconds,
            public string statsJson { get; set; }
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

        public sealed class SheetMap : ClassMap<PlayerData>
        {
            public SheetMap()
            {
				Map(m => m.gameID).Name("Game ID");
				Map(m => m.gameLength).Name("Game Length");
				Map(m => m.RIOT_ID_GAME_NAME).Name("Player");
				Map(m => m.SKIN).Name("Champion");
				Map(m => m.CHAMPIONS_KILLED).Name("Kills");
				Map(m => m.NUM_DEATHS).Name("Deaths");
				Map(m => m.ASSISTS).Name("Assists");
				Map(m => m.TEAM).Name("Team");
				Map(m => m.WIN).Name("Win/Loss");
				Map(m => m.MINIONS_KILLED).Name("CS");
				Map(m => m.GOLD_EARNED).Name("Gold Earned");
			}
        }

		public static string FormatDuration(long milliseconds)
		{
			TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
			int hours = time.Hours;
			int minutes = time.Minutes;
			int seconds = time.Seconds;

			if (time.TotalHours >= 1)
			{
				return $"{hours}h {minutes}m {seconds}s";
			}
			else
			{
				return $"{minutes}m {seconds}s";
			}
		}
		public static Task LoadReplayFile(string replayDir, string replayFile)
		{
            // go through all of the files inside the directory, make sure they're not executables, make sure they're valid, add them to a list of valid files, then parse them
            if (!File.Exists(replayFile))
            {
                return Task.Run(() =>
                {
                    Console.WriteLine("file doesn't exist.");
                });
            }
            else
            {
                return Task.Run(async () =>
                {
                    //await fileLock.WaitAsync();
                    try
                    {
                        // Console.WriteLine("Starting batchcount: " + batchCount);
                        using (StreamReader fs = new StreamReader(replayFile))
                        {
#if SHEETBUILDDEBUG
							Console.WriteLine("DEBUG: " + replayPath);
#endif
                            string fileBlob = fs.ReadToEnd();
                            int startIndex = fileBlob.IndexOf("\"gameLength\"");

                            if (startIndex == -1)
                            {
                                Console.WriteLine("Start of data invalid.");
                                return;
                            }

                            fileBlob = "{" + fileBlob.Substring(startIndex);
                            // rofl files have junk at the end of the file..
                            int lastBrace = fileBlob.LastIndexOf("}");

                            if (lastBrace == -1)
                            {
                                Console.WriteLine("End of file invalid");
                                return;
                            }

                            fileBlob = fileBlob.Substring(0, lastBrace+1);

							GameData gameData = JsonSerializer.Deserialize<GameData>(fileBlob);
                            string gameID = Path.GetFileNameWithoutExtension(replayFile);

                            // double check just to be sure
                            if (gameData == null)
                            {
                                Console.WriteLine("messed up sorting out the end");
                                return;
                            }

                            var players = JsonSerializer.Deserialize<List<PlayerData>>(gameData.statsJson);
                            if (players == null)
                            {
                                Console.WriteLine("something went really wrong here");
                                return;
                            }

                            string realgamelength = FormatDuration(gameData.gameLength);

							// Add in the per game data fields and clean up naming scheme from Riot
							foreach (var p in players)
                            {
                                p.gameID = gameID;
                                p.gameLength = realgamelength;
                                if (p.TEAM == "100") p.TEAM = "RED";
                                else                 p.TEAM = "BLUE";

                                if (p.WIN == "Fail") p.WIN = "Loss";

							}

                            string outputName = replayDir + gameID + ".csv";
#if SHEETBUILDDEBUG
							foreach (var p in players)
                            {
                                Console.WriteLine($"{p.RIOT_ID_GAME_NAME} ({p.SKIN}) " +
                                                    $"Kills: {p.CHAMPIONS_KILLED}, Deaths: {p.NUM_DEATHS}, Assists: {p.ASSISTS}, " +
                                                    $"Minions: {p.MINIONS_KILLED}, Gold: {p.GOLD_EARNED}, Team: {p.TEAM}, Win: {p.WIN}");
                            }
#endif

                            File.Create(outputName).Dispose();
							using (var writer = new StreamWriter(outputName, append: false)) // set append to true if we are adding stuff to the same file / dealing with concurrency
							using (var csvWrite = new CsvWriter(writer, CultureInfo.InvariantCulture))
							{
								csvWrite.Context.RegisterClassMap<SheetMap>();
								csvWrite.WriteRecords(players);
								csvWrite.Flush();
							}
						}
                        //fileLock.Release();
                        //System.IO.File.Move(replayPath, replayPath + "done");
#if SHEETBUILDDEBUG
						Console.WriteLine("Done with " + replayPath);
#endif
                    }
					catch (JsonException ex)
					{
						Console.WriteLine($"JSON Parsing error: {ex.Message}");
					}
					catch (IOException ex)
					{
						Console.WriteLine($"File IO error: {ex.Message}");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Unexpected error: {ex.Message}");
					}
				});
            }
		}
    }
}
