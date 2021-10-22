using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
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

        private string BannedUsersFileName = "bannedusers.txt";
        private List<string> BannedUsers = new List<string>();
        private List<string> Admins = new List<string>() { "Alenael#1801", "Arisu#7114" };

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

                //Load Blocked Users
                LoadBlockedUsers();

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

        private void LoadBlockedUsers()
        {
            if (File.Exists(BannedUsersFileName))
            {
                var bannedUsersAllText = File.ReadAllText(BannedUsersFileName);

                if (bannedUsersAllText != "")
                {
                    var bannedUsers = bannedUsersAllText.Split(",");

                    foreach (var bannedUser in bannedUsers)
                        BannedUsers.Add(bannedUser);
                }
            }
            else            
                File.WriteAllText(BannedUsersFileName, "");            
        }

        private void SaveBlockedUsers()
        {
            var text = String.Join(",", BannedUsers);
            File.WriteAllText(BannedUsersFileName, text);
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

        // This is not the recommended way to write a bot - consider
        // reading over the Commands Framework sample.
        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // The bot should never respond to itself.
            if (message.Author.Id == _client.CurrentUser.Id)
                return;

            if (message.Content.StartsWith("!dx2banlist") && Admins.Contains(message.Author.ToString()))
            {
                if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)
                    if (BannedUsers.Count == 0)
                        await chnl.SendMessageAsync("No Users Banned at this time!");
                    else
                        await chnl.SendMessageAsync(String.Join(",", BannedUsers));
            }
            else if (message.Content.StartsWith("!dx2ban") && Admins.Contains(message.Author.ToString()))
            {
                var items = message.Content.Split("!dx2ban");

                if (BannedUsers.Contains(items[1].Trim()))
                {
                    if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)
                        await chnl.SendMessageAsync($"{items[1].Trim()} is already banned.");
                }
                else
                {
                    await Logger.LogAsync($"{items[1].Trim()} was banned by {message.Author.ToString()}");
                    BannedUsers.Add(items[1].Trim());
                    SaveBlockedUsers();

                    if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)
                        await chnl.SendMessageAsync($"{items[1].Trim()} has been banned from using bot! :(");
                }
            }
            else if (message.Content.StartsWith("!dx2unban") && Admins.Contains(message.Author.ToString()))
            {                
                var items = message.Content.Split("!dx2unban");
                if (BannedUsers.Contains(items[1].Trim()))
                {
                    await Logger.LogAsync($"{items[1].Trim()} was un banned by {message.Author.ToString()}");
                    BannedUsers.Remove(items[1].Trim());
                    SaveBlockedUsers();
                    if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)
                        await chnl.SendMessageAsync($"{items[1].Trim()} has been un banned from using bot! :)");
                }
                else
                {
                    if (_client.GetChannel(message.Channel.Id) is IMessageChannel chnl)
                        await chnl.SendMessageAsync($"{items[1].Trim()} has not been banned.");
                }
            }



            if (BannedUsers.Contains(message.Author.ToString()))
            {
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
