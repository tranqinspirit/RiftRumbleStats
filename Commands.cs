using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.VisualBasic.ApplicationServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.DataFormats;

namespace RiftRumbleStats
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private static FileHandling _filehandling;
        public static IEmote yesreact = new Emoji("✅");
        public static IEmote noreact = new Emoji("⛔");
        private ulong guildId;
		public static readonly HttpClient fileclient = new HttpClient();
        private static string fileclientDir;
        private static string csvPath;
		//private IServiceProvider services;

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
				if (Context.Message.Author.Id != 102920670630916096)
				{
					Console.WriteLine("Not allowed to use replay commands.");
					return;
				}

                //0) make sure we have a csv file to actually output stuff to
                // TODO: this should actually just be generated after getting all the stuff together, then create a new file/report
                if (!File.Exists(csvPath))
                {
                    await ReplyAsync("Couldn't find csv file.");
                }
                else
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

					await ReplyAsync("Found " + fileNameList.Count() + " files.");

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
                                    using var fs = new FileStream(fileclientDir + fileNameList[index], FileMode.CreateNew);

									{
                                        await s.CopyToAsync(fs);
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
			}
			[Command("loadreplays")]
            public async Task LoadReplays()
            {
            	if (Context.Message.Author.Id != 102920670630916096)
				{
					Console.WriteLine("Not allowed to use replay commands.");
					return;
				}
                string badFileFolder = "badfiles";

				Directory.CreateDirectory(fileclientDir + badFileFolder);
				DirectoryInfo dir = new DirectoryInfo(fileclientDir);
				FileInfo[] files = dir.GetFiles();

				IList<Task> FileTaskList = new List<Task>();

				int fCount = Directory.GetFiles(fileclientDir, "*.rofl", SearchOption.TopDirectoryOnly).Length;
                for (int i = 0; i < fCount; i++)
                {
					FileTaskList.Add(RiftRumbleStats.FileHandling.LoadReplayFile(i, files[i].ToString(), csvPath, badFileFolder));
				}

				await Task.WhenAll(FileTaskList);
                await ReplyAsync("Done with file batch.");
			}
		}

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
                // If our command was invalid, we should catch an ApplicationCommandException. This exception contains the path of the error as well as the error message. You can serialize the Error field in the exception to get a visual of where your error is.
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                // You can send this error somewhere or just print it to the console, for this example we're just going to print it.
                Console.WriteLine(json);
            }
        }

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands, string filePath)
        {
            _commands = commands;
            _client = client;
			fileclientDir = filePath + "test";
			csvPath = fileclientDir + "\\test.csv";
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
            if (message == null) return;

            int argPos = 0; // check the ! in the first char of the message

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            // TODO get the channel from a configuration file or something
            if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos) || 
                message.Author.IsBot ||
                message.Channel.Id != 1365079374529040384))
            {
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
