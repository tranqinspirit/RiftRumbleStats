using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// Primarily backend functionality
class Program
{
    private static DiscordSocketClient _client;
    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    public static async Task Main()
    {
        _client = new DiscordSocketClient();

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