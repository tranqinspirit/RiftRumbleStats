using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RRS
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
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
        }
        [Group("file")]
        public class FileModule : ModuleBase<SocketCommandContext>
        {
            // need to store file names somehow to make sure we don't do duplicate
            // checkmark files that we have done stuff with?
        }

        // finalized commands for mods
        [RequireUserPermission(GuildPermission.Administrator, Group = "BotTest")]
        public class AdminModule : ModuleBase<SocketCommandContext>
        {
            [Command("permtest")]
            public async Task BotTest(SocketUser user = null)
            {
                var userInfo = user ?? Context.User;
                await ReplyAsync($"{userInfo.Username}" + " has correct test permissions.");
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
                return;

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
