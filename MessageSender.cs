using Discord;
using Discord.WebSocket;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    class MessageSender : RetrieverBase
    {
        #region Properties

        private List<string> botSpamChannelNames = new List<string>() { "bot-spam", "spam-bot" };
        private List<string> newsChannelNames = new List<string>() { "news", "announcements" };

        #endregion

        #region Constructor

        //Constructor
        public MessageSender(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2alert";
        }

        #endregion

        #region Overrides
        public async override Task ReadyAsync()
        {
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);            

            if (message.Author.ToString() == "Alenael#1801" && message.Content.StartsWith(MainCommand))
            {
                var items = message.Content.Split(MainCommand);

                var alert = items[1].Split("|");

                if (alert.Count() == 2)
                {
                    foreach (var g in _client.Guilds)
                    {
                        var newsChn = false;
                        var botSpam = false;

                        ulong botSpamChnlId = 0;
                        ulong newsChnlId = 0;

                        foreach (var c in g.Channels)
                        {
                            if (!botSpam)
                            {
                                botSpam = botSpamChannelNames.Any(s => s == c.Name);
                                if (botSpam)
                                    botSpamChnlId = c.Id;
                            }
                            if (!newsChn)
                            {
                                newsChn = newsChannelNames.Any(s => s == c.Name);
                                if (newsChn)
                                    newsChnlId = c.Id;
                            }
                        }

                        SocketTextChannel chnl = null;
                        bool canSend = false;

                        if (botSpam && botSpamChnlId != 0)
                        {
                            chnl = _client.GetChannel(botSpamChnlId) as SocketTextChannel;
                            if (chnl != null)
                                canSend = g.CurrentUser.GetPermissions(chnl).SendMessages;
                        }
                        if (!canSend && newsChn && newsChnlId != 0)
                        {
                            chnl = _client.GetChannel(newsChnlId) as SocketTextChannel;
                            if (chnl != null)
                                canSend = g.CurrentUser.GetPermissions(chnl).SendMessages;
                        }

                        if (chnl != null && canSend)
                        {
                            try
                            {
                                await chnl.SendMessageAsync($"**{alert[0]}**\n{alert[1]}");

                                await Logger.LogAsync("Sending Message to '" + g.Name + "' in channel '" + chnl.Name + "'");
                            }
                            catch (Exception e)
                            {
                                await Logger.LogAsync("Could not send to '" + g.Name + "' in channel '" + chnl.Name + "'. " + e.Message + " " + e.InnerException);
                            }
                        }
                    }
                }
            }
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "";
        }

        #endregion
    }
}