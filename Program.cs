using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Dx2_DiscordBot
{
    class Program
    {
        //Used to minimize
        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

        //Our Discord Client
        private DiscordSocketClient _client;
        
        //List of retrievers for us to make use of for data consumption
        private List<RetrieverBase> Retrievers = new List<RetrieverBase>();

        public static bool IsRunning = false;
        
        //Main Entry Point
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            //Only allow 1 instance of our program to run
            if (Program.IsRunning == false)
            {
                //Minimize our application
                ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 6);

                // It is recommended to Dispose of a client when you are finished
                // using it, at the end of your app's lifetime.
                _client = new DiscordSocketClient();
                Logger.SetupLogger();

                _client.Log += Logger.LogAsync;
                _client.Ready += ReadyAsync;
                _client.MessageReceived += MessageReceivedAsync;

                //Add all our Retrievers to our list
                Retrievers.Add(new DemonRetriever(_client));
                Retrievers.Add(new SkillRetriever(_client));
                Retrievers.Add(new AG2Retriever(_client));
                Retrievers.Add(new ResistsRetriever(_client));
                Retrievers.Add(new MoonRetriever(_client));
                Retrievers.Add(new TierDataRetriever(_client));
                Retrievers.Add(new FormulaRetriever(_client));
                Retrievers.Add(new MessageSender(_client));
                Retrievers.Add(new SwordRetriever(_client));
                Retrievers.Add(new ShieldRetriever(_client));

                //Environment.SetEnvironmentVariable("token", "EnterYourTokenHereAndThenUncommentAndRunTHENREMOVE", EnvironmentVariableTarget.User); 

                //Or simply create the environment variable called token with your token as the value
                // Tokens should be considered secret data, and never hard-coded.
                await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("token", EnvironmentVariableTarget.User));
                await _client.StartAsync();


                // Block the program until it is closed.
                await Task.Delay(-1);
            }
        }

        // The Ready event indicates that the client has opened a
        // connection and it is now safe to access the cache.
        private async Task ReadyAsync()
        {
            if (Program.IsRunning == false)
            {
                await Logger.LogAsync($"{_client.CurrentUser} is connected!");

                //Set what we are playing
                await _client.SetGameAsync("!dx2help for Commands");

                //Allow each Retriever to initialize
                foreach (var retriever in Retrievers)
                    await retriever.ReadyAsync();

                Program.IsRunning = true;
            }
        }

        private List<ulong> BannedUsers = new List<ulong>() { 241520603977351168 };

        // This is not the recommended way to write a bot - consider
        // reading over the Commands Framework sample.
        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == _client.CurrentUser.Id)
                return;

            if (BannedUsers.Contains(message.Author.Id))
            {
                //if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)                
                //    await chnl.SendMessageAsync("Nah!");              
                await Logger.LogAsync(message.Author.Username + " tried to use a command but was denied.");
                return;
            }

            if (message.Channel is IPrivateChannel)
            {
                var channelId = message.Channel.Id;

                //Allow each Retriever to check for messages
                foreach (var retriever in Retrievers)
                    await retriever.MessageReceivedAsync(message, "DM", channelId);

                //Returns list of commands
                if (message.Content == "!dx2help")
                    await SendCommandsAsync(message.Channel.Id);
            }
            else if (message.Channel is ISocketMessageChannel)
            {
                //Grab some data about this message specifically
                var serverName = ((SocketGuildChannel)message.Channel).Guild.Name;
                var channelId = message.Channel.Id;

                //Allow each Retriever to check for messages
                foreach (var retriever in Retrievers)
                    await retriever.MessageReceivedAsync(message, serverName, channelId);

                //Returns list of commands
                if (message.Content == "!dx2help")
                    await SendCommandsAsync(message.Channel.Id);
            }
        }

        //Sends a list of commands to the server
        private async Task SendCommandsAsync(ulong id)
        {
            string message = "```md\nSomething not working? DM darkseraphim#1801 on Discord or contact u/AlenaelReal on reddit for help.\n" +
                             "\nCommands:" +
                             "\n* !dx2help - Displays list of commands";

            //Ask each Retriever to print their commands to our list
            foreach (var retriever in Retrievers)
            {
                var messageToAdd = retriever.GetCommands();

                if ((message + messageToAdd + "```").Length > 1999)
                {
                    if (_client.GetChannel(id) is IMessageChannel chnl)
                        await chnl.SendMessageAsync(message + "```");
                    else
                        await Logger.LogAsync("Failed to send Commands" + id);

                    message = "```md\n";
                }
                
                message += messageToAdd;
            }

            if (message != "```md\n")
            {
                if (_client.GetChannel(id) is IMessageChannel chnl)
                    await chnl.SendMessageAsync(message + "```");
                else
                    await Logger.LogAsync("Failed to send Commands" + id);
            }
        }
    }
}
