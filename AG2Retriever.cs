using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class AG2Retriever : RetrieverBase
    {
        #region Properties

        #endregion

        #region Constructor

        //Constructor
        public AG2Retriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!ag2";
        }

        #endregion

        #region Overrides 

        //Initialization
        public async override Task ReadyAsync()
        {
            await Task.Run(() => Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "map\\"));
            await Task.Run(() => Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "boss\\"));            
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);

            if (_client.GetChannel(channelId) is IMessageChannel chnl)
            { 
                if (message.Attachments.Count >= 1)
                {
                    var attachment = message.Attachments.First();

                    if (attachment.Filename.EndsWith(".jpg") || attachment.Filename.EndsWith(".png"))
                    {
                        if (message.Content.StartsWith(MainCommand + "bossupload"))
                        {
                            var items = message.Content.Split(MainCommand + "bossupload");

                            var dir = AppDomain.CurrentDomain.BaseDirectory + "boss\\";
                            var fileName = dir + "SPOILER_" + items[1].Trim() + Path.GetExtension(attachment.Filename);

                            File.Delete(dir + "SPOILER_" + items[1].Trim() + ".png");
                            File.Delete(dir + "SPOILER_" + items[1].Trim() + ".jpg");

                            await DownloadFile(new Uri(attachment.Url), fileName);
                            await chnl.SendMessageAsync("Saved - " + attachment.Filename + " as " + Path.GetFileName(fileName) + " for floor " + items[1].Trim() + ".");
                        }

                        if (message.Content.StartsWith(MainCommand + "mapupload"))
                        {
                            var items = message.Content.Split(MainCommand + "mapupload");

                            var dir = AppDomain.CurrentDomain.BaseDirectory + "map\\";                                
                            var fileName = dir + items[1].Trim() + Path.GetExtension(attachment.Filename);

                            File.Delete(dir + items[1].Trim() + ".png");
                            File.Delete(dir + items[1].Trim() + ".jpg");

                            await DownloadFile(new Uri(attachment.Url), fileName);
                            await chnl.SendMessageAsync("Saved - " + attachment.Filename + " as " + Path.GetFileName(fileName) + " for floor " + items[1].Trim() + ".");
                        }
                    }
                }
                else if (message.Content.StartsWith(MainCommand + "bosslist"))
                {
                    var dir = AppDomain.CurrentDomain.BaseDirectory + "boss\\";
                    var dirFiles = new DirectoryInfo(dir).GetFiles();

                    var files = new StringBuilder();

                    files.Append("```md\n");
                    foreach (var file in dirFiles)
                        files.Append(file.Name.Replace("SPOILER_", "") + "\n");
                    files.Append("```");

                    await chnl.SendMessageAsync(files.ToString() );
                }
                else if (message.Content.StartsWith(MainCommand + "maplist"))
                {
                    var dir = AppDomain.CurrentDomain.BaseDirectory + "map\\";
                    var dirFiles = new DirectoryInfo(dir).GetFiles();

                    var files = new StringBuilder();

                    files.Append("```md\n");
                    foreach (var file in dirFiles)
                        files.Append(file.Name + "\n");
                    files.Append("```");

                    await chnl.SendMessageAsync(files.ToString());
                }
                else if (message.Content.StartsWith(MainCommand + "map"))
                {
                    var items = message.Content.Split(MainCommand + "map");
                    var file = GetFile("map\\", items[1].Trim());

                    if (file != "" && File.Exists(file))
                        await chnl.SendFileAsync(file, "AG2 Map - " + items[1].Trim());
                    else
                        await chnl.SendMessageAsync("Could not find map for that floor. Upload it yourself using !ag2mapupload# and adding an attachment.");
                }

                else if (message.Content.StartsWith(MainCommand + "boss"))
                {
                    var items = message.Content.Split(MainCommand + "boss");
                    var file = GetFile("boss\\", "SPOILER_" + items[1].Trim());

                    if (file != "" && File.Exists(file))
                        await chnl.SendFileAsync(file, "AG2 Boss - " + items[1].Trim());
                    else
                        await chnl.SendMessageAsync("Could not find boss for that floor. Upload it yourself using !ag2bossupload# and adding an attachment.");
                }
            }
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nAG2 Commands: (Only PNG and JPG images are accepted)" +
            "\n* " + MainCommand + "map# - Returns an image of the map for the floor requested." +
            "\n* " + MainCommand + "boss# - Returns an image of the bosses stats for the floor requested." +
            "\n* " + MainCommand + "bossupload# - Allows you to upload an image for a Bosses stats in AG2. Uploading again will overwrite by floor." +
            "\n* " + MainCommand + "mapupload# - Allows you to upload an image for a Map in AG2. Uploading again will overwrite by floor." +
            "\n* " + MainCommand + "maplist - Returns a list of all maps." +
            "\n* " + MainCommand + "bosslist - Returns a list of all bosses.";
        }

        #endregion

        #region Public Methods

        //Gets file sin directory that match our search string
        public static string GetFile(string dir, string fileName)
        {
            var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + dir,
                fileName + ".*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")).ToList();

            if (files.Count >= 1)            
                return files[0];            

            return "";
        }

        //Downloads an image file for us
        public static async Task DownloadFile(Uri url, string fileName)
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFileAsync(url, fileName);
            }
            catch(Exception e)
            {
                await Logger.LogAsync("Couldn't download image. " + e.Message);
            }            
        }

        #endregion

    }
}
