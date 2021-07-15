using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class DemonRetriever : RetrieverBase
    {
        #region Properties

        public static List<Demon> Demons;
        private static List<Rank> Ranks;

        private const int LEV_DISTANCE = 1;

        #endregion

        #region Constructor

        //Constructor
        public DemonRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2demon";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            var demonsDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/SMT Dx2 Database - Demons.csv");

            var tempDemons = new List<Demon>();
            foreach(DataRow row in demonsDt.Rows)            
                tempDemons.Add(LoadDemon(row));                        
            Ranks = GetRanks(tempDemons);
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

                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    //Try to find demon
                    var demon = Demons.Find(d => d.Name.ToLower() == searchedDemon);

                    //If exact demon not found
                    if (demon.Name == null)
                    {
                        //Find anyone matching the nickname of a demon
                        demon = Demons.Find(d => d.Nicknames != "" && d.NicknamesList.Any(n => n == searchedDemon));

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
                                    await chnl.SendMessageAsync("Could not find: " + searchedDemon, false);
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
                    else              
                        await chnl.SendMessageAsync("", false, demon.WriteToDiscord());
                }
            }
        }

        //Gets ranks for selected demons
        public List<Rank> GetRanks(List<Demon> demons)
        {
            var ranks = new List<Rank>();

            foreach(var demon in demons)
            {
                var r = new Rank() { Name = demon.Name };

                var sortedDemons = demons.OrderByDescending(c => c.Str).ToList();
                r.Str = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                sortedDemons = demons.OrderByDescending(c => c.Mag).ToList();
                r.Mag = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                sortedDemons = demons.OrderByDescending(c => c.Vit).ToList();
                r.Vit = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                sortedDemons = demons.OrderByDescending(c => c.Luck).ToList();
                r.Luck = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                sortedDemons = demons.OrderByDescending(c => c.HP).ToList();
                r.HP = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                sortedDemons = demons.OrderByDescending(c => c.Agi).ToList();
                r.Agility = sortedDemons.FindIndex(a => a.Name == demon.Name) + 1;

                ranks.Add(r);
            }            

            return ranks;
        }

        //Cheat to allow Linq in struct
        public static Rank GetMyRank(string name)
        {
            return Ranks.Find(r => r.Name == name);
        }

        //Find demons starting with 
        private List<string> FindDemonsStartingWith(string searchedDemon)
        {                       
            List<string> demonSW = new List<string>();

            foreach(Demon demon in Demons)            
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

            foreach (Demon demon in Demons)
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
            return "\n\nDemon Commands:" +
            "\n* " + MainCommand + " [Demon Name] - Search's for a demon with the name you provided as [Demon Name]. If nothing is found you will recieve a message back stating Demon was not found. Alternate demons can be found like.. Shiva A, Nekomata A. ☆ can be interperted as * when performing searches like... Nero*, V*";
        }

        #endregion

        #region Public Methods

        //Fixes an issue where skills can be the same name of a demon at times
        public static string FixSkillsNamedAsDemons(string name)
        {
            var newName = name;

            if (newName != "")
            {
                foreach (var d in Demons)
                {
                    if (d.Name == newName)
                    {
                        newName = newName + " (Skill)";
                        return newName;
                    }
                }
            }

            return newName;
        }

        //Returns a list of demons that have a skill allows for expandability later on via different sets of data points
        public static Dictionary<string, List<Demon>> GetDemonsWithInnateSkill(string skillName)
        {
            var fixedName = FixSkillsNamedAsDemons(skillName);

            var demons = new Dictionary<string, List<Demon>>();

            var transferableDemons = Demons.Where(
                d => d.Skill1 == fixedName ||
                d.Skill2 == fixedName ||
                d.Skill3 == fixedName ||
                d.AwakenC == fixedName ||
                d.AwakenP == fixedName ||
                d.AwakenR == fixedName ||
                d.AwakenT == fixedName ||
                d.AwakenY == fixedName).ToList();

            demons.Add("Innate", transferableDemons);

            return demons;
        }

        //Returns a list of demons that have a skill allows for expandability later on via different sets of data points
        public static Dictionary<string, List<Demon>> GetDemonsWithSkill(string skillName)
        {
            var fixedName = FixSkillsNamedAsDemons(skillName);

            var demons = new Dictionary<string, List<Demon>>();
            
            var transferableDemons = Demons.Where(
                d => d.Skill1 == fixedName ||
                d.GachaP == fixedName ||
                d.GachaR == fixedName ||
                d.GachaY == fixedName ||
                d.GachaT == fixedName).ToList();

            demons.Add("Transferrable", transferableDemons);

            return demons;
        }

        //Creates a demon object from a data grid view row
        public static Demon LoadDemon(DataRow row)
        {
            var demonVersions = "";
            var name = row["Name"] is DBNull ? "" : (string)row["Name"];

            if (!(row["Alternate Name"] is DBNull))
            {
                var alternateDemon = (string)row["Alternate Name"];
                demonVersions = "{{DemonVersions|" + name + "|" + alternateDemon + "}}\r\n";
            }

            var demon = new Demon();

            demon.Name = name;
            demon.Rarity = row["Rarity"] is DBNull ? "" : (string)row["Rarity"];
            demon.Race = row["Race"] is DBNull ? "" : (string)row["Race"];
            demon.Ai = row["Type"] is DBNull ? "" : (string)row["Type"];
            demon.Grade = row["Grade"] is DBNull ? "" : (string)row["Grade"];

            demon.Str = row["6★ Strength"] is DBNull ? 0 : Convert.ToInt32(row["6★ Strength"]);
            demon.Mag = row["6★ Magic"] is DBNull ? 0 : Convert.ToInt32(row["6★ Magic"]);
            demon.Vit = row["6★ Vitality"] is DBNull ? 0 : Convert.ToInt32(row["6★ Vitality"]);
            demon.Agi = row["6★ Agility"] is DBNull ? 0 : Convert.ToInt32(row["6★ Agility"]);
            demon.Luck = row["6★ Luck"] is DBNull ? 0 : Convert.ToInt32(row["6★ Luck"]);

            demon.DemonVersions = demonVersions;

            demon.Fire = LoadResist(row["Fire"] is DBNull ? "" : (string)row["Fire"]);
            demon.Dark = LoadResist(row["Dark"] is DBNull ? "" : (string)row["Dark"]);
            demon.Light = LoadResist(row["Light"] is DBNull ? "" : (string)row["Light"]);
            demon.Elec = LoadResist(row["Elec"] is DBNull ? "" : (string)row["Elec"]);
            demon.Ice = LoadResist(row["Ice"] is DBNull ? "" : (string)row["Ice"]);
            demon.Force = LoadResist(row["Force"] is DBNull ? "" : (string)row["Force"]);
            demon.Phys = LoadResist(row["Phys"] is DBNull ? "" : (string)row["Phys"]);

            demon.Skill1 = row["Skill 1"] is DBNull ? "" : (string)row["Skill 1"];
            demon.Skill2 = row["Skill 2"] is DBNull ? "" : (string)row["Skill 2"];
            demon.Skill3 = row["Skill 3"] is DBNull ? "" : (string)row["Skill 3"];

            demon.AwakenC = row["Clear Awaken"] is DBNull ? "" : (string)row["Clear Awaken"];
            demon.AwakenR = row["Red Awaken"] is DBNull ? "" : (string)row["Red Awaken"];
            demon.AwakenP = row["Purple Awaken"] is DBNull ? "" : (string)row["Purple Awaken"];
            demon.AwakenY = row["Yellow Awaken"] is DBNull ? "" : (string)row["Yellow Awaken"];
            demon.AwakenT = row["Teal Awaken"] is DBNull ? "" : (string)row["Teal Awaken"];

            demon.GachaR = row["Red Gacha"] is DBNull ? "" : (string)row["Red Gacha"];
            demon.GachaP = row["Purple Gacha"] is DBNull ? "" : (string)row["Purple Gacha"];
            demon.GachaY = row["Yellow Gacha"] is DBNull ? "" : (string)row["Yellow Gacha"];
            demon.GachaT = row["Teal Gacha"] is DBNull ? "" : (string)row["Teal Gacha"];

            demon.Panel1 = row["Panel 1"] is DBNull ? "" : (string)row["Panel 1"];
            demon.Panel2 = row["Panel 2"] is DBNull ? "" : (string)row["Panel 2"];
            demon.Panel3 = row["Panel 3"] is DBNull ? "" : (string)row["Panel 3"];
            demon.Panel4 = row["Panel 4"] is DBNull ? "" : (string)row["Panel 4"];

            demon.Panel1Stats = row["Panel 1 Stats"] is DBNull ? "" : (string)row["Panel 1 Stats"];
            demon.Panel2Stats = row["Panel 2 Stats"] is DBNull ? "" : (string)row["Panel 2 Stats"];
            demon.Panel3Stats = row["Panel 3 Stats"] is DBNull ? "" : (string)row["Panel 3 Stats"];
            demon.Panel4Stats = row["Panel 4 Stats"] is DBNull ? "" : (string)row["Panel 4 Stats"];

            demon.Gacha = row["Gacha"] is DBNull ? false : (string)row["Gacha"] == "1";
            demon.Event = row["Event"] is DBNull ? false : (string)row["Event"] == "1";
            demon.MultiFusion = row["Multi-Fusion"] is DBNull ? false : (string)row["Multi-Fusion"] == "1";
            demon.BannerRequired = row["Banner Required"] is DBNull ? false : (string)row["Banner Required"] == "1";

            demon.Nicknames = row["Nickname"] is DBNull ? "" : (string)row["Nickname"];
            demon.NicknamesList = new List<string>();

            if (demon.Nicknames.Contains(","))
            {
                var nicknameList = demon.Nicknames.Split(",");
                foreach (var nickname in nicknameList)
                    demon.NicknamesList.Add(nickname.Trim());
            }
            else
            {
                demon.NicknamesList.Add(demon.Nicknames.Trim());
            }

            return demon;
        }

        //Returns a value based on what its passed
        private static string LoadResist(string value)
        {
            if (value == "" || value == null)
                return "";

            return value.First().ToString().ToUpper() + value.Substring(1);
        }

        #endregion
    }
    #region Structs

    //Object to hold Demon Data
    public struct Demon
    {
        public string Name;
        public string Race;
        public string Grade;
        public string Rarity;
        public string Ai;
        public int Str;
        public int Mag;
        public int Vit;
        public int Agi;
        public int Luck;
        public string Phys;
        public string Fire;
        public string Ice;
        public string Elec;
        public string Force;
        public string Light;
        public string Dark;
        public int HP { get { return (int)(Vit * 4.7 + 50 * 7.4); } }
        public int PAtk { get { return (int)(Str * 2.1 + 50 * 5.6 + 50); } }
        public int MAtk { get { return (int)(Mag * 2.1 + 50 * 5.6 + 50); } }
        public int PDef { get { return (int)(Vit * 1.1 + Str * 0.5 + 50 * 5.6 + 50); } }
        public int MDef { get { return (int)(Vit * 1.1 + Mag * 0.5 + 50 * 5.6 + 50); } }
        public string DemonVersions;

        public string Skill1;
        public string Skill2;
        public string Skill3;

        public string AwakenC;
        public string AwakenR;
        public string AwakenT;
        public string AwakenP;
        public string AwakenY;

        public string GachaR;
        public string GachaP;
        public string GachaT;
        public string GachaY;

        public string Awaken1;
        public string Awaken2;
        public string Awaken3;
        public string Awaken4;

        public string Awaken1Amount;
        public string Awaken2Amount;
        public string Awaken3Amount;
        public string Awaken4Amount;

        public string Panel1;
        public string Panel2;
        public string Panel3;
        public string Panel4;

        public string Panel1Stats;
        public string Panel2Stats;
        public string Panel3Stats;
        public string Panel4Stats;

        public bool Gacha;
        public bool Event;
        public bool MultiFusion;
        public bool BannerRequired;

        public string Nicknames;
        public List<string> NicknamesList;

        public Embed WriteToDiscord()
        {
            var url = "https://dx2wiki.com/index.php/" + Uri.EscapeDataString(Name);
            var thumbnail = "https://raw.githubusercontent.com/Alenael/Dx2DB/master/Images/Demons/" + Uri.EscapeDataString(Name.Replace("☆", "")) + ".jpg";
            
            var eb = new EmbedBuilder();
            eb.WithTitle(Name);

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

            var panelInfo1 = "";
            var panelInfo2 = "";
            var panelInfo3 = "";
            var panelInfo4 = "";

            if (Panel1 != "")
                panelInfo1 = "1: " + Panel1 + " " + Panel1Stats + "\n";

            if (Panel2 != "")
                panelInfo2 = "2: " + Panel2 + " " + Panel2Stats + "\n";

            if (Panel3 != "")
                panelInfo3 = "3: " + Panel3 + " " + Panel3Stats + "\n";

            if (Panel4 != "")
                panelInfo4 = "4: " + Panel4 + " " + Panel4Stats + "\n\n";

            var clear = "C: " + GenerateSkillWikiLink(AwakenC) + "\n";
            var red = "R: " + GenerateSkillWikiLink(AwakenR) + " | " + GenerateSkillWikiLink(GachaR) + "\n";
            var yellow = "Y: " + GenerateSkillWikiLink(AwakenY) + " | " + GenerateSkillWikiLink(GachaY) + "\n";
            var teal = "T: " + GenerateSkillWikiLink(AwakenT) + " | " + GenerateSkillWikiLink(GachaT) + "\n";
            var purple = "P: " + GenerateSkillWikiLink(AwakenP) + " | " + GenerateSkillWikiLink(GachaP) + "\n\n";

            if (AwakenC == "")
                clear = "";

            if (red.EndsWith(" | \n"))
                red = red.Replace(" | ", "");

            if (AwakenR == "")
                red = "";

            if (yellow.EndsWith(" | \n"))
                yellow = yellow.Replace(" | ", "");

            if (AwakenY == "")
                yellow = "";

            if (teal.EndsWith(" | \n"))
                teal = teal.Replace(" | ", "");

            if (AwakenT == "")
                teal = "";

            if (purple.EndsWith(" | \n\n"))
                purple = purple.Replace(" | ", "");

            if (AwakenP == "")
                purple = "";

            var skill3 = "";

            if (Skill3 != "")
                skill3 = GenerateSkillWikiLink(Skill3) + "\n";
            
            eb.AddField("Skills:",
                 GenerateSkillWikiLink(Skill1) + "\n" +
                 GenerateSkillWikiLink(Skill2) + "\n" +
                 skill3, true);

            eb.AddField("Awaken | Gacha:",
                clear +
                red +
                yellow +
                teal +
                purple, true);

            eb.AddField("Resists", resist, false);

            if (panelInfo1 != "")
                eb.AddField("Panels", panelInfo1 + panelInfo2 + panelInfo3 + panelInfo4, true);

            var demonCount = DemonRetriever.Demons.Count();

            var fusionUrls = "";

            fusionUrls += "[Used In Fusions](" + GetFusionUrl("fusion") + ")\n";

            if (MultiFusion || !Gacha)
                fusionUrls += "[How To Fuse](" + GetFusionUrl("fission") + ")";

            eb.AddField("Stats", "HP: " + HP + " | " +
                "Vit: " + Vit + " (" + DemonRetriever.GetMyRank(Name).Vit + "/" + demonCount + ")\n" +
                "Str: " + Str + " (" + DemonRetriever.GetMyRank(Name).Str + "/" + demonCount + ") | " +
                "Mag: " + Mag + " (" + DemonRetriever.GetMyRank(Name).Mag + "/" + demonCount + ")\n" +
                "Agi: " + Agi + " (" + DemonRetriever.GetMyRank(Name).Agility + "/" + demonCount + ") | " +
                "Luck: " + Luck + " (" + DemonRetriever.GetMyRank(Name).Luck + "/" + demonCount + ")\n\n" +
                fusionUrls
                , true);

            var nicknames = "";

            if (!string.IsNullOrEmpty(Nicknames))
                nicknames = " | Nicknames: " + Nicknames.Replace(",", ", ");

            //Other Info
            eb.WithFooter(
                "Race: " + Race +
                " | Grade: " + Grade +
                " | Rarity: " + Rarity +
                " | Ai: " + Ai +
                nicknames);
            eb.WithColor(Color.Red);
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }
        
        private string GetFusionUrl(string type)
        {
            var newName = Name;

            Regex regex = new Regex(@"\b A\b");
            newName = regex.Replace(newName, " [Dimensional]");

            return "https://oceanxdds.github.io/dx2_fusion/?route="+ type + "&demon=" + Uri.EscapeUriString(newName) + "#en";
        }

        private string GenerateSkillWikiLink(string skill)
        {
            var newDemon = DemonRetriever.FixSkillsNamedAsDemons(skill);

            if (newDemon == "")
                return "";

            newDemon = "[" + newDemon.Replace("(", "").Replace(")", "") + "](https://dx2wiki.com/index.php/" + Uri.EscapeUriString(newDemon.Replace("[", "(").Replace("]", ")")).Replace("(", "%28").Replace(")", "%29") + ")";
            return newDemon;
        }

        public string GenerateDemonWikiLink()
        {
            return "[" + Name + "](https://dx2wiki.com/index.php/" + Uri.EscapeUriString(Name) + ")";
        }
    }

    //Object to hold Rank Data
    public struct Rank
    {
        public string Name;
        public int Str;
        public int Mag;
        public int Vit;
        public int Luck;
        public int Agility;
        public int HP;
    }

    #endregion
}