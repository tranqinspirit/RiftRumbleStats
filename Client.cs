//#define STARTUPDEBUG

using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

// Primarily backend functionality
namespace RiftRumbleStats
{
    class Client
    {
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static ClientData _clientData;

        public class ClientData
        {
            public string accessToken {get;set;}
            public List<ulong> modChannels {get;set;}
            public List<ulong> modUsers {get;set;}
            public string fileDir {get;set;}
		}

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

            // Get the client going
            try
            {
                string configFile = "Config.json";
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), configFile);

                if (File.Exists(configPath))
                {
                    string jsonFile = File.ReadAllText(configFile);
                    _clientData = JsonSerializer.Deserialize<ClientData>(jsonFile);
#if STARTUPDEBUG
                    Console.WriteLine($"Token: {_clientData.accessToken}");
                    foreach (var channel in _clientData.modChannels)
                    {
                        Console.WriteLine("Mod channel: " + channel);
                    }
					foreach (var user in _clientData.modUsers)
					{
						Console.WriteLine("Mod channel: " + user);
					}

                    Console.WriteLine("filedir: " + _clientData.fileDir);
#endif
                }
                else
                {
                    Console.WriteLine("No config file found.");
                    return;
                }

                await _client.LoginAsync(TokenType.Bot, _clientData.accessToken);
                await _client.StartAsync();

                // Initiate the command handler
                _commands = new CommandService();
                CommandHandler handler = new RiftRumbleStats.CommandHandler(_client, _commands);
                await handler.InstallCommandsAsync();

                // SLASH COMMANDS for grabbing stuff?

                // Blocks the task until the program closes.
                await Task.Delay(-1);
            }
            catch (Exception)
            {
                Console.WriteLine("Couldn't find token. Exiting.");
                return;
            }
        }
    }
}