using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

// Primarily backend functionality
namespace RiftRumbleStats
{
    class Client
    {
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        public static async Task Main()
        {
            // Actually enable the gatewayintents..
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.Guilds | GatewayIntents.MessageContent | GatewayIntents.DirectMessages
            };
            _client = new DiscordSocketClient(config);

            _client.Log += Log;

            try
            {
                String token;
                using (StreamReader reader = new("filepath")) // plug in correct filepath to not error out
                {
                    token = reader.ReadToEnd();
                }

                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();

                // check perms

                // Initiate the command handler
                _commands = new CommandService();
                CommandHandler handler = new RiftRumbleStats.CommandHandler(_client, _commands);
                await handler.InstallCommandsAsync();

                // TODO: initialize the sheet
                // SLASH COMMANDS for grabbing stuff?

                // Blocks the task until the program closes.
                await Task.Delay(-1);
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't find token. Exiting.");
                return;
            }
        }
    }
}