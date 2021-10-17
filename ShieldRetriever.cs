using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class ShieldRetriever : RetrieverBase
    {
        #region Properties

        public static List<Shield> Shields;
        private const int LEV_DISTANCE = 1;

        #endregion

        #region Constructor

        //Constructor
        public ShieldRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2shield";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            var ShieldsDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/SMT Dx2 Database - Shields.csv");

            var tempShields = new List<Shield>();
            foreach(DataRow row in ShieldsDt.Rows)
                tempShields.Add(LoadShield(row));
            Shields = tempShields;
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
                string searchedShield = items[1].Trim().Replace("*", "☆").ToLower();

                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    //Try to find demon
                    var Shield = Shields.Find(d => d.DemonName.ToLower() == searchedShield);

                    //If exact demon not found
                    if (Shield.DemonName == null)
                    {
                        //Find anyone matching the nickname of a demon
                        Shield = Shields.Find(d => d.Nicknames != "" && d.NicknamesList.Any(n => n == searchedShield));

                        if (Shield.DemonName == null)
                        {
                            //Find all similar demons
                            List<String> similarShields = GetSimilarShields(searchedShield, LEV_DISTANCE);

                            //If no similar demons found
                            if (similarShields.Count == 0)
                            {
                                List<string> ShieldStartingWith = new List<string>();

                                ShieldStartingWith = FindShieldsStartingWith(searchedShield);

                                if (ShieldStartingWith.Count == 1)
                                {
                                    Shield = Shields.Find(x => x.DemonName.ToLower() == ShieldStartingWith[0].ToLower());
                                    if (Shield.DemonName != null)
                                        await chnl.SendMessageAsync("", false, Shield.WriteToDiscord());
                                }
                                else if (ShieldStartingWith.Count > 1)
                                {
                                    string answerString = "Could not find: " + searchedShield + ". Did you mean: ";

                                    foreach (string fuzzyShield in ShieldStartingWith)
                                    {
                                        answerString += fuzzyShield + ", ";
                                    }

                                    //Remove last space and comma
                                    answerString = answerString.Remove(answerString.Length - 2);

                                    answerString += "?";

                                    await chnl.SendMessageAsync(answerString, false);
                                }
                                else
                                {
                                    await chnl.SendMessageAsync("Could not find: " + searchedShield, false);
                                }
                            }
                            //If exactly 1 demon found, return its Info
                            else if (similarShields.Count == 1)
                            {
                                //Find exactly this demon
                                Shield = Shields.Find(x => x.DemonName.ToLower() == similarShields[0].ToLower());
                                if (Shield.DemonName != null)
                                    await chnl.SendMessageAsync("", false, Shield.WriteToDiscord());
                            }
                            //If similar demons found
                            else
                            {
                                //Build answer string
                                string answerString = "Could not find: " + searchedShield + ". Did you mean: ";

                                foreach (string fuzzyShield in similarShields)
                                {
                                    answerString += fuzzyShield + ", ";
                                }

                                //Remove last space and comma
                                answerString = answerString.Remove(answerString.Length - 2);

                                answerString += "?";

                                await chnl.SendMessageAsync(answerString, false);
                            }
                        }
                        else
                            await chnl.SendMessageAsync("", false, Shield.WriteToDiscord());
                    }
                    else              
                        await chnl.SendMessageAsync("", false, Shield.WriteToDiscord());
                }
            }
        }

        //Find demons starting with 
        private List<string> FindShieldsStartingWith(string searchedShield)
        {                       
            List<string> ShieldSW = new List<string>();

            foreach(Shield Shield in Shields)            
                if (Shield.DemonName.ToLower().StartsWith(searchedShield.ToLower()))
                    ShieldSW.Add(Shield.DemonName);            

            return ShieldSW;
        }

        /// <summary>
        /// Returns List of Shields whose name have a Levinshtein Distance of LEV_DISTANCE
        /// </summary>
        /// <param name="searchedShield">Name of the Shield that is being compared agianst</param>
        /// <returns></returns>
        private List<string> GetSimilarShields(string searchedShield, int levDist)
        {
            List<string> simShields = new List<string>();

            foreach (Shield Shield in Shields)
            {
                int levDistance = 999;

                try
                {
                    levDistance = LevenshteinDistance.EditDistance(Shield.DemonName.ToLower(), searchedShield);
                }
                catch (ArgumentNullException e)
                {
                    Logger.LogAsync("ArgumentNullException in getSimilarDemons: " + e.Message);
                }

                //If only off by levDist characters, add to List
                if (levDistance <= levDist)                
                    simShields.Add(Shield.DemonName);                
            }
            
            return simShields;
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nShield Commands:" +
            "\n* " + MainCommand + " [Demon Name] - Search's for a Shield with the demon name you provided as [Demon Name]. If nothing is found you will recieve a message back stating Shield was not found.";
        }

        //Returns a value based on what its passed
        private static string LoadResist(string value)
        {
            if (value == "" || value == null)
                return "";

            return value.First().ToString().ToUpper() + value.Substring(1);
        }

        #endregion

        #region Public Methods

        //Creates a demon object from a data grid view row
        public static Shield LoadShield(DataRow row)
        {
            var Shield = new Shield();

            Shield.DemonName = row["demon name"] is DBNull ? "" : (string)row["demon name"];
            Shield.ShieldName = row["Shield Name"] is DBNull ? "" : (string)row["Shield Name"];
            Shield.Talent = row["talent name"] is DBNull ? "" : (string)row["talent name"];
            Shield.Effect = row["effect"] is DBNull ? "" : (string)row["effect"];

            Shield.Skill1 = row["skill 1"] is DBNull ? "" : (string)row["skill 1"];
            Shield.Skill2 = row["skill 2"] is DBNull ? "" : (string)row["skill 2"];
            Shield.AwakenSkill = row["Awaken Skill"] is DBNull ? "" : (string)row["Awaken Skill"];
            
            Shield.PDef = row["Defense"] is DBNull ? "" : (string)row["Defense"];
            Shield.Attribute = row["DMG reduction for atribute"] is DBNull ? "" : (string)row["DMG reduction for atribute"];
            Shield.MDef = row["mdef"] is DBNull ? "" : (string)row["mdef"];
            Shield.HP = row["HP%"] is DBNull ? "" : (string)row["HP%"];

            Shield.Panel1 = row["panel 1"] is DBNull ? "" : (string)row["panel 1"];
            Shield.Panel2 = row["panel 2"] is DBNull ? "" : (string)row["panel 2"];
            Shield.Panel3 = row["panel 3"] is DBNull ? "" : (string)row["panel 3"];

            Shield.Panel1Stats = row["Panel 1 Step"] is DBNull ? "" : (string)row["Panel 1 Step"];
            Shield.Panel2Stats = row["panel 2 Step"] is DBNull ? "" : (string)row["panel 2 Step"];
            Shield.Panel3Stats = row["panel 3 Step"] is DBNull ? "" : (string)row["panel 3 Step"];

            Shield.Nicknames = row["Nickname"] is DBNull ? "" : (string)row["Nickname"];
            Shield.NicknamesList = new List<string>();

            if (Shield.Nicknames.Contains(","))
            {
                var nicknameList = Shield.Nicknames.Split(",");
                foreach (var nickname in nicknameList)
                    Shield.NicknamesList.Add(nickname.Trim());
            }
            else
            {
                Shield.NicknamesList.Add(Shield.Nicknames.Trim());
            }

            Shield.Fire = LoadResist(row["fire"] is DBNull ? "" : (string)row["fire"]);
            Shield.Dark = LoadResist(row["dark"] is DBNull ? "" : (string)row["dark"]);
            Shield.Light = LoadResist(row["light"] is DBNull ? "" : (string)row["light"]);
            Shield.Elec = LoadResist(row["elec"] is DBNull ? "" : (string)row["elec"]);
            Shield.Ice = LoadResist(row["ice"] is DBNull ? "" : (string)row["ice"]);
            Shield.Force = LoadResist(row["force"] is DBNull ? "" : (string)row["force"]);
            Shield.Phys = LoadResist(row["phys"] is DBNull ? "" : (string)row["phys"]);

            return Shield;
        }

        #endregion
    }
    #region Structs

    //Object to hold Demon Data
    public struct Shield
    {
        public string DemonName;
        public string ShieldName;
        public string Talent;
        public string Effect;
        public string Skill1;
        public string Skill2;
        public string AwakenSkill;
        public string Panel1;
        public string Panel2;
        public string Panel3;
        public string Panel1Stats;
        public string Panel2Stats;
        public string Panel3Stats;

        public string PDef;
        public string MDef;
        public string Attribute;
        public string HP;

        public string Phys;
        public string Fire;
        public string Ice;
        public string Elec;
        public string Force;
        public string Light;
        public string Dark;

        public string Nicknames;
        public List<string> NicknamesList;

        public Embed WriteToDiscord()
        {
            var url = "https://dx2wiki.com/index.php/" + Uri.EscapeDataString(ShieldName);
            var thumbnail = "https://raw.githubusercontent.com/Alenael/Dx2DB/master/Images/Shields/" + Uri.EscapeDataString(ShieldName.Replace("☆", "")) + ".jpg";

            var eb = new EmbedBuilder();
            eb.WithTitle(ShieldName);

            eb.AddField("Demon Name:", DemonName, true);

            eb.AddField("Skills:",
            Skill1 + "\n" +
            Skill2 + "\n" +
            AwakenSkill, true);

            eb.AddField(Talent + ":", Effect, false);
            
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

            var resist = "";

            if (Phys != "")
                resist += " | Phys: " + Phys + " ";

            if (Fire != "")
                resist += " | Fire: " + Fire + " ";

            if (Ice != "")
                resist += " | Ice: " + Ice + " ";

            if (Elec != "")
                resist += " | Elec: " + Elec + " ";

            if (Force != "")
                resist += " | Force: " + Force + " ";

            if (Light != "")
                resist += " | Light: " + Light + " ";

            if (Dark != "")
                resist += " | Dark: " + Dark;

            if (resist.Length > 0)
                resist = resist.Remove(0, 3);

            eb.AddField("Resists", resist, false);

            ////Other Info
            eb.WithFooter(Attribute +
                " | HP%: " + HP +
                " | PDef: " + PDef +
                " | MDef: " + MDef);
            eb.WithColor(Color.Red);
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }
    }

    #endregion
}