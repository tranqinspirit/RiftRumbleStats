using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RiftRumbleStats
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private static FileHandling _filehandling;
        //private IServiceProvider services;

        /* Template for commands
        // OPTIONAL: [GROUP("<name>"] // this groups the commands but I'm not really sure how yet
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
                List<string> userReacts;
                bool haveReacted = false;
                
                foreach (var x in messages)
                {
                    if (!haveReacted)
                    {
                        foreach (var y in x.Reactions.Values)
                        {
                            if (y.IsMe)
                            {
                                haveReacted = true;
                                await ReplyAsync("Already reacted.");
                                break;
                            }
                            else 
                            {
                                IEmote yesreact = new Emoji("✅");
                                IEmote noreact = new Emoji("⛔");
                                await Context.Message.AddReactionsAsync(new[] { yesreact });
                                Console.WriteLine("Adding reaction to " + x.Id);
                            }
                        }
                    }
                }

                if (!haveReacted)
                    await ReplyAsync("Haven't reacted to any of these yet.");
            }
        }
        [Group("file")]
        public class FileModule : ModuleBase<SocketCommandContext>
        {
            [Command("setup")]
            public async Task FileSetup()
            {
                // set up file managing stuff
                bool success = false;
                try
                {
                    _filehandling = new FileHandling(success);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Couldn't set up file handling.");
                }
                if (success)
                    await ReplyAsync("File handling enabled.");
                else
                    await ReplyAsync("Failed to set up file handling.");
            }
            
            // need to store file names somehow to make sure we don't do duplicate
        }

        // finalized commands for mods
        [RequireUserPermission(GuildPermission.Administrator, Group = "BotTest")]
        public class AdminModule : ModuleBase<SocketCommandContext>
        {
            IEmote yesreact = new Emoji("✅");
            IEmote noreact = new Emoji("⛔");

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
                switch (RiftRumbleStats.FileHandling.CheckFileExt(Context.Message))
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
        }

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;
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
