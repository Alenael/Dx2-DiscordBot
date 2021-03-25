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
    class NewsRetriever : RetrieverBase
    {
        #region Properties

        private HtmlWeb newsPageWeb = new HtmlWeb();
        List<News> NewsItems;
        private string baseUrl = "https://d2-megaten-l.sega.com/en/news/";
        System.Timers.Timer timer = null;

        #endregion

        #region Constructor

        //Constructor
        public NewsRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2news";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            NewsItems = LoadNewsFromFile();
            ResetTimer();
            await PrepareToSendNews();
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);

            if (message.Author.ToString() == "Alenael#1801" && message.Content.StartsWith(MainCommand))
            {
                var items = message.Content.Split(MainCommand);

                if (items[1].Trim().StartsWith("test"))
                {
                    if (_client.GetChannel(channelId) is IMessageChannel chnl)
                    {
                        _ = SendNews(new News() { Title = "Kishin/Snake Fusion 30% OFF Event Coming Soon! (This is a manual test of the system. Thanks for the understanding.)", Url = "https://d2-megaten-l.sega.com/en/news/detail/079492.html", Image = "https://d2-megaten-l.sega.com/webview/en/upload_images/848669ee2d7bc251692a9f264817eb62cc7ee313.png" }, true);
                    }
                }
                else if (items[1].Trim().StartsWith("send it"))
                {
                    if (_client.GetChannel(channelId) is IMessageChannel chnl)
                    {
                        foreach (var news in NewsItems)
                            if (news.Sent == false)
                            {
                                _ = Logger.LogAsync($"New News Posted! {news.Title}");
                                _ = SendNews(news);
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

        #region Public Methods

        public void ResetTimer()
        {
            timer = new System.Timers.Timer(600000);
            timer.Elapsed += Timer_Elapsed;
            timer.Enabled = true;
        }

        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ResetTimer();
            await PrepareToSendNews();
        }

        public async Task PrepareToSendNews()
        {
            var newNewsItems = await GetNewsItems();

            if (NewsItems.Count() != 0)
            {
                foreach (var ni in newNewsItems)
                {
                    if (!NewsItems.Any(i => i.Url == ni.Url) && !ni.Sent)
                    {
                        _ = Logger.LogAsync($"New News Posted! {ni.Title}");
                        _ = SendNews(ni);
                        NewsItems.Add(ni);

                        await Task.Delay(15000);
                    }
                }
            }
            else
            {
                NewsItems = newNewsItems;

                foreach (var item in newNewsItems)
                    item.Sent = true;
            }

            SaveNewsToFile();
        }

        private List<string> botSpamChannelNames = new List<string>() { "bot-spam", "spam-bot" };
        private List<string> newsChannelNames = new List<string>() { "news", "announcements" };

        public async Task SendNews(News ni, bool testing = false)
        {
            var fileName = $"{Guid.NewGuid()}.png";

            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                if (!String.IsNullOrEmpty(ni.Image))
                    client.DownloadFileTaskAsync(new Uri(ni.Image), fileName).Wait();

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

                    if (newsChn && newsChnlId != 0)
                    {
                        chnl = _client.GetChannel(newsChnlId) as SocketTextChannel;
                        if (chnl != null)
                            canSend = g.CurrentUser.GetPermissions(chnl).SendMessages &&
                                g.CurrentUser.GetPermissions(chnl).EmbedLinks &&
                                g.CurrentUser.GetPermissions(chnl).AttachFiles;
                    }
                    if (!canSend && botSpam && botSpamChnlId != 0)
                    {
                        chnl = _client.GetChannel(botSpamChnlId) as SocketTextChannel;
                        if (chnl != null)
                            canSend = g.CurrentUser.GetPermissions(chnl).SendMessages &&
                                g.CurrentUser.GetPermissions(chnl).EmbedLinks &&
                                g.CurrentUser.GetPermissions(chnl).AttachFiles;
                    }

                    if (chnl != null && canSend)
                    {
                        try
                        {
                            if (!String.IsNullOrEmpty(ni.Image) && File.Exists(fileName))
                                await chnl.SendFileAsync(fileName, $"**{ni.Title}**\n{ni.Url}");
                            else
                                await chnl.SendMessageAsync($"**{ni.Title}**\n{ni.Url}");

                            await Logger.LogAsync("Sending News to '" + g.Name + "' in channel '" + chnl.Name + "'");
                        }
                        catch (Exception e)
                        {
                            await Logger.LogAsync("Could not send to '" + g.Name + "' in channel '" + chnl.Name + "'. " + e.Message + " " + e.InnerException);
                        }
                    }

                    await Task.Delay(5000);
                }
            }

            if (!String.IsNullOrEmpty(ni.Image))
                File.Delete(fileName);

            ni.Sent = true;
            SaveNewsToFile();
        }

        private const string newsItemsFileName = "newsItems.json";

        public void SaveNewsToFile()
        {
            var settings = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            var jsonString = JsonSerializer.Serialize(NewsItems, settings);
            File.WriteAllTextAsync(newsItemsFileName, jsonString);
        }

        public List<News> LoadNewsFromFile()
        {
            if (File.Exists(newsItemsFileName))
            {
                var jsonString = File.ReadAllText(newsItemsFileName);
                return JsonSerializer.Deserialize<List<News>>(jsonString);
            }
            else
                return new List<News>();
        }

        public async Task<List<News>> GetNewsItems()
        {
            var document = await newsPageWeb.LoadFromWebAsync($"https://d2-megaten-l.sega.com/en/news/index.html");

            var urls = document.DocumentNode.SelectNodes("//*[@class='news-list-title']/a");
            var info = document.DocumentNode.SelectNodes("//*[@class='newslist-hed cf']");

            var newsItems = new List<News>();

            for (int i = 0; i < urls.Count; i++)
            {
                var link = urls[i].GetAttributeValue("href", "");

                var newsItem = new News();
                newsItem.Title = urls[i].SelectSingleNode("h3").InnerText.Replace("\"", "\"\"") ?? "";
                newsItem.Url = baseUrl + link ?? "";
                newsItem.Image = urls[i].SelectSingleNode("div/img")?.GetAttributeValue("src", "") ?? "";
                newsItems.Add(newsItem);
            }

            return newsItems;
        }

        public Embed WriteToDiscord(News newsItem)
        {
            var eb = new EmbedBuilder();

            eb.Title = newsItem.Title;
            eb.Description = newsItem.Url;
            eb.ImageUrl = newsItem.Image;

            return eb.Build();
        }

        #endregion
    }

    [Serializable]
    public class News
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Image { get; set; }
        public bool Sent { get; set; }
    }
}