using CsvHelper;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RiftRumbleStats.Client;
using static RiftRumbleStats.FileHandling;

namespace RiftRumbleStats
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly ClientData _clientData;
        public static IEmote yesreact = new Emoji("✅");
        public static IEmote noreact = new Emoji("⛔");
		public static readonly HttpClient fileclient = new HttpClient();
        private static string fileclientDir;

		/* Template for commands
        // OPTIONAL: [GROUP("<name>"] // adds command parameters in the sent message
        // [Command]("name", RunMode = <runmode>)] // eg: RunMode.Async
        // [Summary("<description>")] // Believe this is just for dev side
        // OPTIONAL: [RequireContexxt(ContextType.<type>)]
        */

		[Group("test")]
        public class TestModule : ModuleBase<SocketCommandContext>
        {
            [Command("say")]
            [Summary("Echoes a message.")]
            public async Task SayAsync([Remainder][Summary("The text to echo")] string echo) => await ReplyAsync(echo);

            [Command("mirror")]
            [Summary("talk to whoever said the message")]
            [Alias("user", "whois")]
            public async Task UserInfoAsync(
            [Summary("The (optional) user to get info from")]
            SocketUser user = null)
            {
                var userInfo = user ?? Context.Client.CurrentUser;
                await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator}");
            }
            [Command("reactcheck")]
            public async Task ReactCheck(int messageCount) 
            {
                var messages = await Context.Message.Channel.GetMessagesAsync(messageCount+1).FlattenAsync();
                bool noReacts = false;
                foreach (var x in messages)
                {
                    foreach (var y in x.Reactions.Values)
                    {
                        if (y.IsMe)
                        {
                            Console.WriteLine("Already reacted.");
                            break;
                        }
                        else
                        {
                            await Context.Message.AddReactionsAsync([yesreact]);
                            Console.WriteLine("Adding reaction to " + x.Id);
                            break;
                        }
                    }
                }
                if (!noReacts)
                    Console.WriteLine("No Reactions.");
            }
        }
        [Group("mod")]
        [RequireRole("BotTest")]
        public class AdminModule : ModuleBase<SocketCommandContext>
        {
            [Command("permtest")]
            public async Task BotTest(SocketUser user = null)
            {
                var userInfo = user ?? Context.User;
                await ReplyAsync($"{userInfo.Username}" + " has correct test permissions.");
            }

            [Command("filecheck")]
            // check channel for files
            public async Task CheckFile()
            { 
                switch (RiftRumbleStats.FileHandling.CheckFileExtDiscord(Context.Message))
                {
                    case (2):
                    {
                       await Context.Message.AddReactionsAsync(new[] { yesreact });
                       await ReplyAsync("File included is a .rofl file.");                        
                    }
                    break;
                    case (1):
                    {
                        await Context.Message.AddReactionsAsync(new[] { noreact });
                        await ReplyAsync("File included is the wrong file type.");                        
                    } break;
                    case (0):
                    {
                        await Context.Message.AddReactionsAsync(new[] { noreact });
                        await ReplyAsync("message does not have an attachment");
                    } break;
                }                                
            }
            [Command("membercheck")]
            public async Task CheckMembers(params string[] memberMsg)
            {
                foreach (string member in memberMsg)
                {          
                    var user = await Context.Guild.SearchUsersAsync(member, memberMsg.Count());
                    if (user.Count > 0)
                        await ReplyAsync("Found " + user.ElementAt(0));
                    else
                        await ReplyAsync("Couldn't find " + member);
                }
			}
            [Command("savereplays")]
            public async Task SaveReplays(int messageCount)
            {              
                List<string> fileNameList = [];
				List<string> urlArray = [];
				var messages = await Context.Message.Channel.GetMessagesAsync(messageCount + 1).FlattenAsync();
				foreach (var x in messages)
				{
                    if (x.Reactions.Values.Count() > 0)
                    {
                        foreach (var y in x.Reactions.Values)
                        {
                            if (!y.IsMe)
                            {
                                await Context.Message.AddReactionsAsync([yesreact]);
                            }
                        }
                    }
                    else
                    {
						await Context.Message.AddReactionsAsync([yesreact]);  
					}
                            					
                    foreach (IAttachment attachment in x.Attachments)
                    {
                        var fileType = attachment.ContentType;
                        if (fileType == null)
                        {
                            var roflTrim = attachment.Filename;
							Match match = Regex.Match(roflTrim, @"\.rofl\b");
							string fixString = match.ToString();
							fixString = fixString.TrimStart('{');
							fixString = fixString.TrimEnd('}');
                            if (fixString.Equals(".rofl"))
                            {
								urlArray.Add(attachment.Url);
                                fileNameList.Add(attachment.Filename);
							}
						}						
                    }
				}

				//await ReplyAsync("Found " + fileNameList.Count() + " files.");

				if (urlArray.Count() > 0)
                {
                    try
                    {
                        foreach (var x in urlArray.Select((value, i) => new { i, value }))
                        {
                            var url = x.value;
                            var index = x.i;
                            using (var s = await fileclient.GetStreamAsync(url))
                            {
                                try
                                {
                                    using var fs = new FileStream(fileclientDir + fileNameList[index], FileMode.CreateNew);
                                    {
                                        Console.WriteLine("Creating file: " + fileclientDir + fileNameList[index]);
										await s.CopyToAsync(fs);
                                    }
                                }
                                catch (IOException ex)
                                {
									Console.WriteLine($"File IO error: {ex.Message}");
								}
                            }
                        }
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine($"Error: {e.Message}");
                    }
				}
			}
			[Command("loadreplays")]
            public async Task LoadReplays()
            {
				DirectoryInfo dir = new DirectoryInfo(fileclientDir);
				FileInfo[] files = dir.GetFiles();

				int fCount = Directory.GetFiles(fileclientDir, "*.rofl", SearchOption.TopDirectoryOnly).Length;

                if (fCount == 0)
                {
                    Console.WriteLine("No replays to process.");
                    await ReplyAsync("No replays to process.");
                }
                else
                {
					IList<Task> FileTaskList = new List<Task>();

					for (int i = 0; i < fCount; i++)
                    {
                        FileTaskList.Add(RiftRumbleStats.FileHandling.LoadReplayFile(fileclientDir, files[i].ToString()));
                    }

                    await Context.Message.AddReactionsAsync([yesreact]);
                    await Task.WhenAll(FileTaskList);
                }
			}

			[Command("batchreport")]
			public async Task ProcessAttachmentsAsync(int messageCount, SocketTextChannel channel, ulong fromMessageId)
			{
				await Context.Message.AddReactionsAsync([yesreact]);
				var messages = await channel.GetMessagesAsync(fromMessageId, Direction.After, limit: messageCount).FlattenAsync();

				var attachmentTasks = messages
					.SelectMany(m => m.Attachments)
					.Where(a => a.Filename.EndsWith(".rofl", StringComparison.OrdinalIgnoreCase))
					.Select(async attachment =>
					{
						using var stream = await fileclient.GetStreamAsync(attachment.Url);
						using var reader = new StreamReader(stream);

						string fileText = await reader.ReadToEndAsync();

						int startIndex = fileText.IndexOf("{\"gameLength\"");
						int endIndex = fileText.LastIndexOf('}');
						if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
							return new List<PlayerData>();

						string jsonBlock = fileText.Substring(startIndex, endIndex - startIndex + 1);

						string gameID = Path.GetFileNameWithoutExtension(attachment.Filename);

						return RiftRumbleStats.FileHandling.ParseReplayJson(jsonBlock, gameID);
					});

				var results = await Task.WhenAll(attachmentTasks);
				var allRecords = results.SelectMany(r => r).ToList();

                foreach (var record in allRecords)
                {
					record.TEAM = record.TEAM == "100" ? "RED" : "BLUE";
                    if (record.WIN == "Fail") record.WIN = "Loss";
				}

				var uniqueRecords = allRecords
					.GroupBy(r => (r.gameID, r.RIOT_ID_GAME_NAME))
					.Select(g => g.First())
					.ToList();

				await using var memoryStream = new MemoryStream();
				await using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
				await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
				{
					csv.Context.RegisterClassMap<SheetMap>();
					csv.WriteRecords(uniqueRecords);
				}
				memoryStream.Position = 0;

				/* debug print
				foreach (var record in uniqueRecords)
				{
					Console.WriteLine($"{record.gameID},{record.gameLength},{record.RIOT_ID_GAME_NAME},...");
				}
                */
                string outputFile = "MemoryBatchReport" + DateTime.Now.ToString("M-d-yyyy") + ".csv";
				// send file to Discord
				await Context.Message.Channel.SendFileAsync(memoryStream, outputFile, "Batch Report Completed.");
			}
		}
        /*
		public async Task Client_Ready(DiscordSocketClient client)
        {
            
            // Let's build a guild command! We're going to need a guild so lets just put that in a variable.
            var guild = client.GetGuild(guildId);

            // Next, lets create our slash command builder. This is like the embed builder but for slash commands.
            var guildCommand = new SlashCommandBuilder();

            // Note: Names have to be all lowercase and match the regular expression ^[\w-]{3,32}$
            guildCommand.WithName("test");

            // Descriptions can have a max length of 100.
            guildCommand.WithDescription("This is my first guild slash command!");

            // Let's do our global command
            var globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("test");
            globalCommand.WithDescription("placeholder");

            try
            {
                // Now that we have our builder, we can call the CreateApplicationCommandAsync method to make our slash command.
                await guild.CreateApplicationCommandAsync(guildCommand.Build());

                // With global commands we don't need the guild.
                await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (HttpException exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
        */

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;

			string configFile = "Config.json";
			string configPath = Path.Combine(Directory.GetCurrentDirectory(), configFile);

			string jsonFile = File.ReadAllText(configFile);
			_clientData = JsonSerializer.Deserialize<ClientData>(jsonFile);
			fileclientDir = _clientData.fileDir + "test\\";
		}

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: null);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) 
                return;

            int argPos = 0; // check the ! in the first char of the message

			// Determine if the message is a command based on the prefix and make sure no bots trigger commands
			if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos) || 
                message.Author.IsBot))
            {
                if (message.HasCharPrefix('!', ref argPos))
                {

                    if (!_clientData.modUsers.Contains(message.Author.Id))
                    {
                        Console.WriteLine($"User not allowed to use commands. : {message.Author.Username}");
                        return;
                    }

                    if (!_clientData.modChannels.Contains(message.Channel.Id))
                    {
                        Console.WriteLine($"Wrong channel for commands. : {message.Channel.Id}");
                        return;
                    }
                }

				return;
            }
            
			// Create a WebSocket-based command context based on the message
			var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: null);
        }
    }
}
