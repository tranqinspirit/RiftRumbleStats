//#define STARTUPDEBUG

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RiftRumbleStats
{
    class Client
    {
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static ClientData _clientData;
		private readonly Dictionary<ulong, List<string>> _pendingMessages = new();

		public class ClientData
        {
            public string accessToken {get;set;}
            public List<ulong> modChannels {get;set;}
            public List<ulong> modUsers {get;set;}
			public List<ulong> botDMLogUsers { get; set; }
			public ulong serverID {get;set;}
            public ulong botDMChannel {get;set;}
            public string fileDir {get;set;}
		}

		// Simple constructor
		public Client()
		{
			// Actually enable the gatewayintents..
			var config = new DiscordSocketConfig
			{
				GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.Guilds | GatewayIntents.MessageContent | GatewayIntents.DirectMessages
			};
			_client = new DiscordSocketClient(config);
		}

		private async Task MessageReceivedAsync(SocketMessage message)
		{
			if (message.Author.IsBot || message.Channel is not IDMChannel dmChannel)
				return;

			ulong userId = message.Author.Id;

			if (_pendingMessages.TryGetValue(userId, out var messages))
			{
				_pendingMessages[userId].Add(message.Content);
			}
			else
			{
				_pendingMessages[userId] = new List<string> { message.Content };

				// Send both buttons: Send + Cancel
				var buttons = new ComponentBuilder()
					.WithButton("✅ Send All Messages", customId: $"send_{userId}", ButtonStyle.Success)
					.WithButton("❌ Cancel", customId: $"cancel_{userId}", ButtonStyle.Danger);

				await dmChannel.SendMessageAsync(
					text: "📝Message received. Press a button below when you're done.",
					components: buttons.Build());
			}
		}

		private async Task HandleButtonAsync(SocketMessageComponent component)
		{
			ulong userId = component.User.Id;

			// Handle Send button (user confirms to send their DM)
			if (component.Data.CustomId == $"send_{userId}")
			{
				if (_pendingMessages.TryGetValue(userId, out var messages))
				{
					var guild = _client.GetGuild(_clientData.serverID);
					var channel = guild?.GetTextChannel(_clientData.botDMChannel);

					if (channel != null)
					{
						string messageId = Guid.NewGuid().ToString().Substring(0, 8);
						string content = string.Join("\n", messages);
						string timestamp = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd hh:mm tt (zzz)");

						var embed = new EmbedBuilder()
							.WithTitle("📥 Private Mod Message")
							.AddField("Message ID", messageId, inline: true)
							.AddField("Sent At", timestamp, inline: false)
							.WithDescription(content)
							.WithColor(Color.Blue)
							.Build();

						var logButton = new ButtonBuilder()
							.WithLabel("Log Info")
							.WithCustomId($"log_{messageId}")
							.WithStyle(ButtonStyle.Primary);

						var componentBuilder = new ComponentBuilder()
							.WithButton(logButton)
							.Build();

						await channel.SendMessageAsync(embed: embed, components: componentBuilder);

						await component.UpdateAsync(msg =>
						{
							msg.Content = "✅ Your message has been sent. Thank you!";
							msg.Components = new ComponentBuilder().Build();
						});

						// Remove pending message after sending
						_pendingMessages.Remove(userId);
					}
					else
					{
						await component.RespondAsync("⚠️ Could not forward your message. Try again later.", ephemeral: true);
					}
				}
				else
				{
					await component.RespondAsync("⚠️ No message session found.", ephemeral: true);
				}
			}
			// Handle Cancel button (user cancels message session)
			else if (component.Data.CustomId == $"cancel_{userId}")
			{
				if (_pendingMessages.ContainsKey(userId))
				{
					_pendingMessages.Remove(userId);

					await component.UpdateAsync(msg =>
					{
						msg.Content = "❌ Your message session has been canceled.";
						msg.Components = new ComponentBuilder().Build();
					});
				}
				else
				{
					await component.RespondAsync("⚠️ No session found to cancel.", ephemeral: true);
				}
			}
			// Handle Log Info button in the server channel embed
			else if (component.Data.CustomId.StartsWith("log_"))
			{
				string messageId = component.Data.CustomId.Substring("log_".Length);

				Console.WriteLine($"[LOG BUTTON CLICKED] Message ID: {messageId} | By User: {component.User} ({component.User.Id}) | At: {DateTime.Now}");

				await component.RespondAsync($"Logged message info for ID: {messageId}", ephemeral: true);

				// Disable the button after click
				var disabledButton = new ButtonBuilder()
					.WithLabel("Log Info")
					.WithCustomId(component.Data.CustomId)
					.WithStyle(ButtonStyle.Secondary)
					.WithDisabled(true);

				var disabledComponent = new ComponentBuilder()
					.WithButton(disabledButton)
					.Build();

				await component.Message.ModifyAsync(msg =>
				{
					msg.Components = disabledComponent;
				});
			}
			else
			{
				await component.RespondAsync("❌ This button isn't for you.", ephemeral: true);
			}
		}

		private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

		// obligatory
		public static async Task Main(string[] args)
		{
			var main = new Client();
			await main.MainAsync();
		}

        public async Task MainAsync()
        {   
            _client.Log += Log;
            _client.MessageReceived += MessageReceivedAsync;
			_client.ButtonExecuted += HandleButtonAsync;

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