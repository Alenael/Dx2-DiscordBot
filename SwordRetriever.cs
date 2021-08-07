using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class SwordRetriever : RetrieverBase
    {
        #region Properties

        public static List<Sword> Swords;
        private const int LEV_DISTANCE = 1;

        #endregion

        #region Constructor

        //Constructor
        public SwordRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2sword";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            var swordsDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/SMT Dx2 Database - Swords.csv");

            var tempSwords = new List<Sword>();
            foreach(DataRow row in swordsDt.Rows)
                tempSwords.Add(LoadSword(row));        
            Swords = tempSwords;
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
                string searchedSword = items[1].Trim().Replace("*", "☆").ToLower();

                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    //Try to find demon
                    var sword = Swords.Find(d => d.DemonName.ToLower() == searchedSword);

                    //If exact demon not found
                    if (sword.DemonName == null)
                    {
                        //Find anyone matching the nickname of a demon
                        sword = Swords.Find(d => d.Nicknames != "" && d.NicknamesList.Any(n => n == searchedSword));

                        if (sword.DemonName == null)
                        {
                            //Find all similar demons
                            List<String> similarSwords = GetSimilarSwords(searchedSword, LEV_DISTANCE);

                            //If no similar demons found
                            if (similarSwords.Count == 0)
                            {
                                List<string> swordStartingWith = new List<string>();

                                swordStartingWith = FindSwordsStartingWith(searchedSword);

                                if (swordStartingWith.Count == 1)
                                {
                                    sword = Swords.Find(x => x.DemonName.ToLower() == swordStartingWith[0].ToLower());
                                    if (sword.DemonName != null)
                                        await chnl.SendMessageAsync("", false, sword.WriteToDiscord());
                                }
                                else if (swordStartingWith.Count > 1)
                                {
                                    string answerString = "Could not find: " + searchedSword + ". Did you mean: ";

                                    foreach (string fuzzySword in swordStartingWith)
                                    {
                                        answerString += fuzzySword + ", ";
                                    }

                                    //Remove last space and comma
                                    answerString = answerString.Remove(answerString.Length - 2);

                                    answerString += "?";

                                    await chnl.SendMessageAsync(answerString, false);
                                }
                                else
                                {
                                    await chnl.SendMessageAsync("Could not find: " + searchedSword, false);
                                }
                            }
                            //If exactly 1 demon found, return its Info
                            else if (similarSwords.Count == 1)
                            {
                                //Find exactly this demon
                                sword = Swords.Find(x => x.DemonName.ToLower() == similarSwords[0].ToLower());
                                if (sword.DemonName != null)
                                    await chnl.SendMessageAsync("", false, sword.WriteToDiscord());
                            }
                            //If similar demons found
                            else
                            {
                                //Build answer string
                                string answerString = "Could not find: " + searchedSword + ". Did you mean: ";

                                foreach (string fuzzySword in similarSwords)
                                {
                                    answerString += fuzzySword + ", ";
                                }

                                //Remove last space and comma
                                answerString = answerString.Remove(answerString.Length - 2);

                                answerString += "?";

                                await chnl.SendMessageAsync(answerString, false);
                            }
                        }
                        else
                            await chnl.SendMessageAsync("", false, sword.WriteToDiscord());
                    }
                    else              
                        await chnl.SendMessageAsync("", false, sword.WriteToDiscord());
                }
            }
        }

        //Find demons starting with 
        private List<string> FindSwordsStartingWith(string searchedSword)
        {                       
            List<string> swordSW = new List<string>();

            foreach(Sword sword in Swords)            
                if (sword.DemonName.ToLower().StartsWith(searchedSword.ToLower()))
                    swordSW.Add(sword.DemonName);            

            return swordSW;
        }

        /// <summary>
        /// Returns List of swords whose name have a Levinshtein Distance of LEV_DISTANCE
        /// </summary>
        /// <param name="searchedSword">Name of the Sword that is being compared agianst</param>
        /// <returns></returns>
        private List<string> GetSimilarSwords(string searchedSword, int levDist)
        {
            List<string> simSwords = new List<string>();

            foreach (Sword sword in Swords)
            {
                int levDistance = 999;

                try
                {
                    levDistance = LevenshteinDistance.EditDistance(sword.DemonName.ToLower(), searchedSword);
                }
                catch (ArgumentNullException e)
                {
                    Logger.LogAsync("ArgumentNullException in getSimilarDemons: " + e.Message);
                }

                //If only off by levDist characters, add to List
                if (levDistance <= levDist)                
                    simSwords.Add(sword.DemonName);                
            }
            
            return simSwords;
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nSword Commands:" +
            "\n* " + MainCommand + " [Demon Name] - Search's for a sword with the demon name you provided as [Demon Name]. If nothing is found you will recieve a message back stating Sword was not found.";
        }

        #endregion

        #region Public Methods
        
        //Creates a demon object from a data grid view row
        public static Sword LoadSword(DataRow row)
        {
            var sword = new Sword();

            sword.DemonName = row["demon name"] is DBNull ? "" : (string)row["demon name"];
            sword.SwordName = row["sword Name"] is DBNull ? "" : (string)row["sword Name"];
            sword.Talent = row["Talent name and effect"] is DBNull ? "" : (string)row["Talent name and effect"];

            sword.Skill1 = row["skill 1"] is DBNull ? "" : (string)row["skill 1"];
            sword.Skill2 = row["skill 2"] is DBNull ? "" : (string)row["skill 2"];
            sword.AwakenSkill = row["awaken skill"] is DBNull ? "" : (string)row["awaken skill"];
            
            sword.AtkStat = row["Phys/mag atk %"] is DBNull ? "" : (string)row["Phys/mag atk %"];
            sword.Attribute = row["Atribute increase"] is DBNull ? "" : (string)row["Atribute increase"];
            sword.Accuracy = row["Phys ACC%"] is DBNull ? "" : (string)row["Phys ACC%"];
            sword.Critical = row["Critical %"] is DBNull ? "" : (string)row["Critical %"];

            sword.Panel1 = row["panel 1"] is DBNull ? "" : (string)row["panel 1"];
            sword.Panel2 = row["panel 2"] is DBNull ? "" : (string)row["panel 2"];
            sword.Panel3 = row["panel 3"] is DBNull ? "" : (string)row["panel 3"];

            sword.Panel1Stats = row["Panel 1 Step"] is DBNull ? "" : (string)row["Panel 1 Step"];
            sword.Panel2Stats = row["panel 2 Step"] is DBNull ? "" : (string)row["panel 2 Step"];
            sword.Panel3Stats = row["panel 3 Step"] is DBNull ? "" : (string)row["panel 3 Step"];

            sword.Nicknames = row["Nickname"] is DBNull ? "" : (string)row["Nickname"];
            sword.NicknamesList = new List<string>();

            if (sword.Nicknames.Contains(","))
            {
                var nicknameList = sword.Nicknames.Split(",");
                foreach (var nickname in nicknameList)
                    sword.NicknamesList.Add(nickname.Trim());
            }
            else
            {
                sword.NicknamesList.Add(sword.Nicknames.Trim());
            }

            return sword;
        }

        #endregion
    }
    #region Structs

    //Object to hold Demon Data
    public struct Sword
    {
        public string DemonName;
        public string SwordName;
        public string Talent;
        public string Skill1;
        public string Skill2;
        public string AwakenSkill;
        public string Panel1;
        public string Panel2;
        public string Panel3;
        public string Panel1Stats;
        public string Panel2Stats;
        public string Panel3Stats;

        public string AtkStat;
        public string Attribute;
        public string Accuracy;
        public string Critical;

        public string Nicknames;
        public List<string> NicknamesList;

        public Embed WriteToDiscord()
        {
            var url = "https://dx2wiki.com/index.php/" + Uri.EscapeDataString(SwordName);
            var thumbnail = "https://raw.githubusercontent.com/Alenael/Dx2DB/master/Images/Swords/" + Uri.EscapeDataString(SwordName.Replace("☆", "")) + ".jpg";
            
            var eb = new EmbedBuilder();
            eb.WithTitle(SwordName);

            eb.AddField("Demon Name:", DemonName, true);

            eb.AddField("Skills:",
            Skill1 + "\n" +
            Skill2 + "\n" +
            AwakenSkill, true);

            eb.AddField("Talent:", Talent, false);
            
            var panelInfo1 = "";
            var panelInfo2 = "";
            var panelInfo3 = "";

            if (Panel1 != "")
                panelInfo1 = "1: " + Panel1 + " " + Panel1Stats + "\n";

            if (Panel2 != "")
                panelInfo2 = "2: " + Panel2 + " " + Panel2Stats + "\n";

            if (Panel3 != "")
                panelInfo3 = "3: " + Panel3 + " " + Panel3Stats + "\n";

            if (panelInfo1 != "")
                eb.AddField("Panels", panelInfo1 + panelInfo2 + panelInfo3, true);

            ////Other Info
            eb.WithFooter(AtkStat +
                " | Acc: " + Accuracy +
                " | Crit: " + Critical);
            eb.WithColor(Color.Red);
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }
    }

    #endregion
}