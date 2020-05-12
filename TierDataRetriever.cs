﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class TierDataRetriever : RetrieverBase
    {
        #region Properties

        public static List<DemonInfo> Demons;

        private const int LEV_DISTANCE = 1;

        #endregion

        #region Constructor

        //Constructor
        public TierDataRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2tier";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            var demonsDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/TierData.csv");

            var tempDemons = new List<DemonInfo>();
            foreach (DataRow row in demonsDt.Rows)
                tempDemons.Add(LoadDemonInfo(row));
            Demons = tempDemons;
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);

            if (message.Content.StartsWith(MainCommand))
            {
                var items = message.Content.Split(MainCommand);

                //Save demon to be searched for
                string searchedDemon = items[1].Trim().Replace("*", "☆").ToLower();

                //Try to find demon
                var demon = Demons.Find(d => d.Name.ToLower() == searchedDemon);

                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    //Find anyone matching the nickname of a demon
                    var demonNickname = DemonRetriever.Demons.Find(d => d.Nicknames != "" && d.NicknamesList.Any(n => n == searchedDemon));

                    if (demonNickname.Name != null)                    
                        demon = Demons.Find(d => d.Name == demonNickname.Name);

                    if (demon.Name == null)
                    {
                        //Find all similar demons
                        List<String> similarDemons = GetSimilarDemons(searchedDemon, LEV_DISTANCE);

                        //If no similar demons found
                        if (similarDemons.Count == 0)
                        {
                            List<string> demonsStartingWith = new List<string>();

                            demonsStartingWith = FindDemonsStartingWith(searchedDemon);

                            if (demonsStartingWith.Count == 1)
                            {
                                demon = Demons.Find(x => x.Name.ToLower() == demonsStartingWith[0].ToLower());
                                if (demon.Name != null)
                                    await chnl.SendMessageAsync("", false, demon.WriteToDiscord());
                            }
                            else if (demonsStartingWith.Count > 1)
                            {
                                string answerString = "Could not find: " + searchedDemon + ". Did you mean: ";

                                foreach (string fuzzyDemon in demonsStartingWith)
                                {
                                    answerString += fuzzyDemon + ", ";
                                }

                                //Remove last space and comma
                                answerString = answerString.Remove(answerString.Length - 2);

                                answerString += "?";

                                await chnl.SendMessageAsync(answerString, false);
                            }
                            else
                            {
                                await chnl.SendMessageAsync("Could not find: " + searchedDemon + " its Tier Info may need to be added to the Wiki first.", false);
                            }
                        }
                        //If exactly 1 demon found, return its Info
                        else if (similarDemons.Count == 1)
                        {
                            //Find exactly this demon
                            demon = Demons.Find(x => x.Name.ToLower() == similarDemons[0].ToLower());
                            if (demon.Name != null)
                                await chnl.SendMessageAsync("", false, demon.WriteToDiscord());
                        }
                        //If similar demons found
                        else
                        {
                            //Build answer string
                            string answerString = "Could not find: " + searchedDemon + ". Did you mean: ";

                            foreach (string fuzzyDemon in similarDemons)
                            {
                                answerString += fuzzyDemon + ", ";
                            }

                            //Remove last space and comma
                            answerString = answerString.Remove(answerString.Length - 2);

                            answerString += "?";

                            await chnl.SendMessageAsync(answerString, false);
                        }
                    }
                    else
                        await chnl.SendMessageAsync("", false, demon.WriteToDiscord());
                }
            }
        }

        //Find demons starting with 
        private List<string> FindDemonsStartingWith(string searchedDemon)
        {
            List<string> demonSW = new List<string>();

            foreach (DemonInfo demon in Demons)
                if (demon.Name.ToLower().StartsWith(searchedDemon.ToLower()))
                    demonSW.Add(demon.Name);

            return demonSW;
        }

        /// <summary>
        /// Returns List of demons whose name have a Levinshtein Distance of LEV_DISTANCE
        /// </summary>
        /// <param name="searchedDemon">Name of the Demon that is being compared agianst</param>
        /// <returns></returns>
        private List<string> GetSimilarDemons(string searchedDemon, int levDist)
        {
            List<string> simDemons = new List<string>();

            foreach (DemonInfo demon in Demons)
            {
                int levDistance = 999;

                try
                {
                    levDistance = LevenshteinDistance.EditDistance(demon.Name.ToLower(), searchedDemon);
                }
                catch (ArgumentNullException e)
                {
                    Logger.LogAsync("ArgumentNullException in getSimilarDemons: " + e.Message);
                }

                //If only off by levDist characters, add to List
                if (levDistance <= levDist)
                    simDemons.Add(demon.Name);
            }

            return simDemons;
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nTier Data Commands:" +
            "\n* " + MainCommand + " [Demon Name] - Search's for a demon with the name you provided as [Demon Name]. If nothing is found you will recieve a message back stating Demon was not found. Alternate demons can be found like.. Shiva A, Nekomata A. ☆ can be interperted as * when performing searches like... Nero*, V*";
        }

        #endregion

        #region Public Methods


        //Creates a demon object from a data grid view row
        public static DemonInfo LoadDemonInfo(DataRow row)
        {
            var name = row["Name"] is DBNull ? "" : (string)row["Name"];

            var demon = new DemonInfo();

            demon.Name = name;
            demon.BestArchetypePvE = row["BestArchetypePvE"] is DBNull ? "" : (string)row["BestArchetypePvE"];
            demon.BestArchetypePvP = row["BestArchetypePvP"] is DBNull ? "" : (string)row["BestArchetypePvP"];
            demon.PvEScore = row["PvEScore"] is DBNull ? "" : (string)row["PvEScore"];
            demon.PvPOffenseScore = row["PvPOffenseScore"] is DBNull ? "" : (string)row["PvPOffenseScore"];
            demon.PvPDefScore = row["PvPDefScore"] is DBNull ? "" : (string)row["PvPDefScore"];
            demon.Pros = row["Pros"] is DBNull ? "" : (string)row["Pros"];
            demon.Cons = row["Cons"] is DBNull ? "" : (string)row["Cons"];

            return demon;
        }

        #endregion
    }
    #region Structs

    //Object to hold Demon Data
    public struct DemonInfo
    {
        public string Name;
        public string BestArchetypePvE;
        public string BestArchetypePvP;
        public string PvEScore;
        public string PvPOffenseScore;
        public string PvPDefScore;
        public string Pros;
        public string Cons;

        internal Embed WriteToDiscord()
        {
            var url = "https://dx2wiki.com/index.php/" + Uri.EscapeDataString(Name) + "/Builds";
            var thumbnail = "https://raw.githubusercontent.com/Alenael/Dx2DB/master/Images/Demons/" + Uri.EscapeDataString(Name.Replace("☆", "")) + ".jpg";

            var pros = "";
            var cons = "";

            if (!string.IsNullOrEmpty(Pros))
                pros = "* " + Pros.Replace("\n", "\n* ");

            if (!string.IsNullOrEmpty(Cons))
                cons = "* " + Cons.Replace("\n", "\n* ");

            var bestArchetypePvE = "";
            if (!string.IsNullOrEmpty(BestArchetypePvE))
            {
                foreach (char ch in BestArchetypePvE)                
                    bestArchetypePvE += ch.ToString() + ", ";

                bestArchetypePvE = bestArchetypePvE.Remove(bestArchetypePvE.Length - 2, 2);
            }

            var bestArchetypePvP = "";
            if (!string.IsNullOrEmpty(BestArchetypePvP))
            {
                foreach (char ch in BestArchetypePvP)                
                    bestArchetypePvP += ch.ToString() + ", ";

                bestArchetypePvP = bestArchetypePvP.Remove(bestArchetypePvP.Length - 2, 2);
            }

            var eb = new EmbedBuilder();
            eb.WithTitle(Name);
            if (!string.IsNullOrEmpty(bestArchetypePvE))
                eb.AddField("PvE Archetype(s)", bestArchetypePvE, true);
            if (!string.IsNullOrEmpty(bestArchetypePvP))
                eb.AddField("PvP Archetype(s)", bestArchetypePvP, true);
            if (!string.IsNullOrEmpty(PvEScore))
                eb.AddField("PvE Rating", PvEScore, true);
            if (!string.IsNullOrEmpty(PvPOffenseScore))
                eb.AddField("PvP Offense Rating", PvPOffenseScore, true);
            if (!string.IsNullOrEmpty(PvPDefScore))
                eb.AddField("PvP Defense Rating", PvPDefScore, true);
            if (!string.IsNullOrEmpty(pros))
                eb.AddField("Pros", pros, false);
            if (!string.IsNullOrEmpty(cons))
                eb.AddField("Cons", cons, false);
            eb.WithFooter("If you disagree with this discuss in #tier-list in Dx2 Liberation Discord Server or update the Wiki page by clicking the demons name at the top.");
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }
    }

    #endregion
}



