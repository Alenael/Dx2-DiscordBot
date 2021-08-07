using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.OleDb;
using Newtonsoft.Json;
using CsvHelper;
using Discord;

namespace Dx2_DiscordBot
{
    public class RetrieverBase
    {
        #region Properties

        //Link to our existing Client
        public DiscordSocketClient _client;

        //Our Main Command for this Retriever
        public string MainCommand = "";

        #endregion

        #region Constructor

        /// <summary>
        /// Base level Constructor for our Retriever
        /// </summary>
        /// <param name="client"></param>
        public RetrieverBase(DiscordSocketClient client)
        {
            _client = client;
        }

        #endregion

        #region Overrides

        //Occurs when Retriever is initializing
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async virtual Task ReadyAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            throw new NotImplementedException();
        }
        
        //Allows Retrievers to manage their own Messages as needed
        public async virtual Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Only record messages that start with Main Command to console for debugging purposes
            if (message.Content.StartsWith(MainCommand))
                await Logger.LogAsync(serverName + " Sent: " + message.Content);

            if (message.Content.StartsWith("!dx2reload"))
                await ReadyAsync();            
        }

        //Returns list of commands for this Retriever
        public virtual string GetCommands()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public Methods

        //Parses a URL in order to retrieve a CSV file and return its data in data table format
        public async Task<DataTable> GetCSV(string url)
        {
            var dt = new DataTable();

            try
            {
                WebClient webClient = new WebClient();
                var results = webClient.DownloadString(url);

                using (var csv = new CsvReader(new StringReader(results)))
                {
                    using (var dr = new CsvDataReader(csv))
                    {
                        var bad = new List<string>();
                        csv.Configuration.BadDataFound = context =>
                        {
                            bad.Add(context.RawRecord);
                        };
                        //while (dr.Read())
                        //{
                        //}
                        dt.Load(dr);
                    }
                }                        
            }
            catch(Exception e)
            {
                await Logger.LogAsync("Failed to Load Url into DataTable. " + e.Message);
            }

            return dt;
        }

        #endregion

        #region Private Methods
        
        #endregion

    }
}
