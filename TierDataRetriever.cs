using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class TierDataRetriever : RetrieverBase
    {
        #region Properties

        public static List<DemonInfo> Demons;
        public static SortedDictionary<int, List<DemonInfo>> PvPOffRatings;
        public static SortedDictionary<int, List<DemonInfo>> PvPDefRatings;
        public static SortedDictionary<int, List<DemonInfo>> PvERatings;
        public static SortedDictionary<int, List<DemonInfo>> DemoPrelimRankings;
        public static SortedDictionary<int, List<DemonInfo>> DemoBossRankings;
        public System.Timers.Timer Timer;

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
            await LoadData();
            Timer = new System.Timers.Timer();
            var now = DateTime.Now;
            var tomorrow = new DateTime(now.Year, now.Month, now.Day, 0, 5, 0).AddDays(1);
            var duration = (tomorrow - now).TotalMilliseconds;
            Timer.Interval = duration;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();
        }

        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Timer.Stop();
            await LoadData();
            Timer.Interval = 86400000;
            Timer.Enabled = true;
        }

        public async Task LoadData()
        {
            var demonsDt = await GetCSV(ConfigurationManager.AppSettings["tierDataPath"]);
            //var demonsDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/TierData.csv");

            var tempDemons = new List<DemonInfo>();
            foreach (DataRow row in demonsDt.Rows)
                tempDemons.Add(LoadDemonInfo(row));
            Demons = tempDemons;

            PvPOffRatings = CreateRankings(Demons, 0);
            PvPDefRatings = CreateRankings(Demons, 1);
            PvERatings = CreateRankings(Demons, 2);
            DemoPrelimRankings = CreateRankings(Demons, 3);
            DemoBossRankings = CreateRankings(Demons, 4);
        }

        public SortedDictionary<int, List<DemonInfo>> CreateRankings(List<DemonInfo> demonInfo, int type)
        {
            var tempDict = new SortedDictionary<int, List<DemonInfo>>();

            for(var i = 4; i < 6; i++)
            {
                var tempList = new List<DemonInfo>();

                foreach (var d in demonInfo)
                {
                    switch (type)
                    {
                        case 0:
                            if (d.PvPOffScoreDbl >= i && d.PvPOffScoreDbl < i + 1)
                                tempList.Add(d);
                            break;
                        case 1:
                            if (d.PvPDefScoreDbl >= i && d.PvPDefScoreDbl < i + 1)
                                tempList.Add(d);
                            break;
                        case 2:
                            if (d.PvEScoreDbl >= i && d.PvEScoreDbl < i + 1)
                                tempList.Add(d);
                            break;
                        case 3:
                            if (d.DemoPrelimScoreDbl >= i && d.DemoPrelimScoreDbl < i + 1)
                                tempList.Add(d);
                            break;
                        case 4:
                            if (d.DemoBossScoreDbl >= i && d.DemoBossScoreDbl < i + 1)
                                tempList.Add(d);
                            break;
                    }
                }

                tempDict.Add(i, tempList);
            }

            return tempDict;
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);

            if (message.Content.StartsWith(MainCommand))
            {
                var items = message.Content.Split(MainCommand);

                if (items[1].Trim().StartsWith("list"))
                {
                    if (_client.GetChannel(channelId) is IMessageChannel chnl)
                    {
                        if (items[1].Trim() == "listdef")
                        {
                            if (PvPDefRatings != null)
                            {
                                var embed = WriteTierListToDiscord(PvPDefRatings, "Top PvP Defense Rankings", 1);
                                await chnl.SendMessageAsync("", false, embed);
                            }
                        }
                        else if (items[1].Trim() == "listpve")
                        {
                            if (PvERatings != null)
                            {
                                var embed = WriteTierListToDiscord(PvERatings, "Top PvE Rankings", 2);
                                await chnl.SendMessageAsync("", false, embed);
                            }
                        }
                        else if (items[1].Trim() == "listdemoprelim")
                        {
                            if (DemoPrelimRankings != null)
                            {
                                var embed = WriteTierListToDiscord(DemoPrelimRankings, "Top Demo Prelim Rankings", 3);
                                await chnl.SendMessageAsync("", false, embed);
                            }
                        }
                        else if (items[1].Trim() == "listdemoboss")
                        {
                            if (DemoBossRankings != null)
                            {
                                var embed = WriteTierListToDiscord(DemoBossRankings, "Top Demo Boss Rankings", 4);
                                await chnl.SendMessageAsync("", false, embed);
                            }
                        }
                        else if (items[1].Trim() == "list")
                        {
                            if (PvPOffRatings != null)
                            {
                                var embed = WriteTierListToDiscord(PvPOffRatings, "Top PvP Offense Rankings", 0);
                                await chnl.SendMessageAsync("", false, embed);
                            }
                        }
                    }
                }
                else
                {
                    //Save demon to be searched for
                    string searchedDemon = items[1].Trim().Replace("*", "☆").ToLower();

                    //Try to find demon
                    var demon = Demons.Find(d => d.Name.ToLower() == searchedDemon);

                    if (_client.GetChannel(channelId) is IMessageChannel chnl)
                    {
                        if (items[1].Trim() == String.Empty)
                        {
                            await chnl.SendMessageAsync("Did you forget to add a demon?");
                        }
                        else
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
            "\n* " + MainCommand + "list - Displays each demon in the top 2 tiers in the PvP Off tier list." +
            "\n* " + MainCommand + "listdef - Displays each demon in the top 2 tiers in the PvP Def tier list." +
            "\n* " + MainCommand + "listpve - Displays each demon in the top 2 tiers in the PvE tier list." +
            "\n* " + MainCommand + "listdemoprelim - Displays each demon in the top 2 tiers in the Demo Prelim list." +
            "\n* " + MainCommand + "listdemoboss - Displays each demon in the top 2 tiers in the Demo Boss list." +
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
            demon.DemoPrelimScore = row["DemoPrelimScore"] is DBNull ? "" : (string)row["DemoPrelimScore"];
            demon.DemoBossScore = row["DemoBossScore"] is DBNull ? "" : (string)row["DemoBossScore"];
            demon.Pros = row["Pros"] is DBNull ? "" : (string)row["Pros"];
            demon.Cons = row["Cons"] is DBNull ? "" : (string)row["Cons"];
            demon.Notes = row["Notes"] is DBNull ? "" : (string)row["Notes"];

            return demon;
        }

        public Embed WriteTierListToDiscord(SortedDictionary<int, List<DemonInfo>> rankings, string title, int type)
        {
            var eb = new EmbedBuilder();
            eb.WithTitle(title);

            foreach(var item in rankings.OrderByDescending(item => item.Key))
            {
                var demonsList = "";

                foreach(var demon in item.Value)
                {
                    switch (type)
                    {
                        case 0:
                            demonsList += $"{demon.Name} ({demon.PvPOffScoreDbl}), ";
                            break;
                        case 1:
                            demonsList += $"{demon.Name} ({demon.PvPDefScoreDbl}), ";
                            break;
                        case 2:
                            demonsList += $"{demon.Name} ({demon.PvEScoreDbl}), ";
                            break;
                        case 3:
                            demonsList += $"{demon.Name} ({demon.DemoPrelimScoreDbl}), ";
                            break;
                        case 4:
                            demonsList += $"{demon.Name} ({demon.DemoBossScoreDbl}), ";
                            break;
                    }
                }

                if (demonsList != "")
                    demonsList = demonsList.Remove(demonsList.Length - 2, 2);

                eb.AddField($"Tier {item.Key}:", demonsList, false);
            }

            eb.WithFooter("If you disagree with this discuss in #tier-list in Dx2 Liberation Discord Server or update the Wiki pages.");
            return eb.Build();
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
        public string DemoPrelimScore;
        public string DemoBossScore;
        public string Pros;
        public string Cons;
        public string Notes;
        public bool FiveStar;

        public double PvEScoreDbl
        {
            get
            {
                double.TryParse(PvEScore, out double dbl);
                return dbl;
            }
        }

        public double PvPDefScoreDbl
        {
            get
            {
                double.TryParse(PvPDefScore, out double dbl);
                return dbl;
            }
        }

        public double PvPOffScoreDbl
        {
            get
            {
                double.TryParse(PvPOffenseScore, out double dbl);
                return dbl;
            }
        }
        public double DemoPrelimScoreDbl
        {
            get
            {
                double.TryParse(DemoPrelimScore, out double dbl);
                return dbl;
            }
        }
        public double DemoBossScoreDbl
        {
            get
            {
                double.TryParse(DemoBossScore, out double dbl);
                return dbl;
            }
        }

        internal Embed WriteToDiscord()
        {
            var url = "https://dx2wiki.com/index.php/Tier_List#" + Uri.EscapeDataString(Name).Replace("%20", "_");
            var thumbnail = "https://raw.githubusercontent.com/Alenael/Dx2DB/master/Images/Demons/" + Uri.EscapeDataString(Name.Replace("☆", "")) + ".jpg";

            var pros = "";
            var cons = "";
            var notes = "";

            if (!string.IsNullOrEmpty(Pros))
                pros = "* " + Pros.Replace("\n", "\n* ");

            if (!string.IsNullOrEmpty(Cons))
                cons = "* " + Cons.Replace("\n", "\n* ");

            if (!string.IsNullOrEmpty(Notes) && Notes != "-")
                notes = "* " + Notes.Replace("\n", "\n* ");

            var bestArchetypePvE = "";
            if (BestArchetypePvE == "Any")
            {
                bestArchetypePvE = "Any";
            }
            else if (!string.IsNullOrEmpty(BestArchetypePvE))
            {
                foreach (char ch in BestArchetypePvE)
                    bestArchetypePvE += ch.ToString() + ", ";

                bestArchetypePvE = bestArchetypePvE.Remove(bestArchetypePvE.Length - 2, 2);
            }

            var bestArchetypePvP = "";
            if (BestArchetypePvP == "Any")
            {
                bestArchetypePvP = "Any";
            }
            else if (!string.IsNullOrEmpty(BestArchetypePvP))
            {
                foreach (char ch in BestArchetypePvP)
                    bestArchetypePvP += ch.ToString() + ", ";

                bestArchetypePvP = bestArchetypePvP.Remove(bestArchetypePvP.Length - 2, 2);
            }

            var description = "";

            if (!string.IsNullOrEmpty(pros))
                description += "Pros:\n" + pros + "\n\n";
            if (!string.IsNullOrEmpty(cons))
                description += "Cons:\n" + cons + "\n\n";
            if (!string.IsNullOrEmpty(notes))
                description += "Notes:\n" + notes;

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
            if (!string.IsNullOrEmpty(DemoPrelimScore))
                eb.AddField("Demo Prelim Rating", DemoPrelimScore, true);
            if (!string.IsNullOrEmpty(DemoBossScore))
                eb.AddField("Demo Boss Rating", DemoBossScore, true);
            eb.WithDescription(description);
            eb.WithFooter("If you disagree with this discuss in #tier-list in Dx2 Liberation Discord Server or update the Wiki page by clicking the demons name at the top.");
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }
    }

    #endregion
}