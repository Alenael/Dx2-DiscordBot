using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class SkillRetriever : RetrieverBase
    {
        #region Properties

        private static List<Skill> Skills;

        private const int LEV_DISTANCE = 1;

        private const int MAX_SIMILAR_SKILLS = 10;

        #endregion

        #region Constructor

        //Constructor
        public SkillRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2skill";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            var skillDt = await GetCSV("https://raw.githubusercontent.com/Alenael/Dx2DB/master/csv/SMT Dx2 Database - Skills.csv");            
            //var skillDt = await GetCSV("https://raw.githubusercontent.com/Maaster/Dx2DB/master/csv/SMT Dx2 Database - Skills.csv");

            var tempSkills = new List<Skill>();
            foreach (DataRow row in skillDt.Rows)
                tempSkills.Add(LoadSkill(row));
            Skills = tempSkills;
        }

        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);
            
            if (message.Content.StartsWith(MainCommand))
            {
                var items = message.Content.Split(MainCommand);

                string searchedSkill = items[1].Trim().ToLower();

                var skill = Skills.Find(s => s.Name.ToLower() == items[1].Trim().ToLower());
                
                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    /*
                    if (skill.Name == null)
                    {
                        await chnl.SendMessageAsync("Could not find: " + items[1].Trim(), false);
                    }
                    else
                        await chnl.SendMessageAsync("", false, skill.WriteToDiscord());                    
                    */
                    //If exact demon not found
                    if (skill.Name == null)
                    {
                        //Find all similar demons
                        List<String> similarDemons = getSimilarSkills(searchedSkill, LEV_DISTANCE);

                        //If no similar demons found
                        if (similarDemons.Count == 0)
                        {
                            List<string> skillsStartingWith = new List<string>();

                            skillsStartingWith = findSkillsStartingWith(searchedSkill);
                            
                            if (skillsStartingWith.Count == 1)
                            {
                                skill = Skills.Find(x => x.Name.ToLower() == skillsStartingWith[0].ToLower());
                                if (skill.Name != null)
                                    await chnl.SendMessageAsync("", false, skill.WriteToDiscord());
                            }
                            else if(skillsStartingWith.Count > MAX_SIMILAR_SKILLS)
                            {
                                await chnl.SendMessageAsync("Could not find: " + searchedSkill + ". More than " + MAX_SIMILAR_SKILLS + " skills that start with this name exists, please refine your search.", false);
                            }
                            else if (skillsStartingWith.Count > 1)
                            {
                                string answerString = "Could not find: " + searchedSkill + ". Did you mean: ";

                                foreach (string fuzzySkill in skillsStartingWith)
                                {
                                    answerString += fuzzySkill + ", ";
                                }

                                //Remove last space and comma
                                answerString = answerString.Remove(answerString.Length - 2);

                                answerString += "?";

                                await chnl.SendMessageAsync(answerString, false);
                            }
                            else
                            {
                                await chnl.SendMessageAsync("Could not find: " + searchedSkill, false);
                            }
                        }
                        //If exactly 1 demon found, return its Info
                        else if (similarDemons.Count == 1)
                        {
                            //Find exactly this demon
                            skill = Skills.Find(x => x.Name.ToLower() == similarDemons[0].ToLower());
                            if (skill.Name != null)
                                await chnl.SendMessageAsync("", false, skill.WriteToDiscord());
                        }
                        //If similar demons found
                        else
                        {
                            //Build answer string
                            string answerString = "Could not find: " + searchedSkill + ". Did you mean: ";

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
                        await chnl.SendMessageAsync("", false, skill.WriteToDiscord());
                }                   
            }
        }

        private List<string> findSkillsStartingWith(string searchedSkill)
        {
            List<string> skillSW = new List<string>();

            foreach (Skill skill in Skills)
            {
                if (skill.Name.ToLower().StartsWith(searchedSkill.ToLower()))
                    skillSW.Add(skill.Name);
            }

            return skillSW;
        }

        /// <summary>
        /// Returns List of demons whose name have a Levinshtein Distance of LEV_DISTANCE
        /// </summary>
        /// <param name="searchedSkill">Name of the Demon that is being compared agianst</param>
        /// <returns></returns>
        private List<string> getSimilarSkills(string searchedSkill, int levDist)
        {
            List<string> simSkills = new List<string>();

            foreach (Skill skill in Skills)
            {
                int levDistance = 999;

                try
                {
                    levDistance = LevenshteinDistance.EditDistance(skill.Name.ToLower(), searchedSkill);
                }
                catch (ArgumentNullException e)
                {
                    Logger.LogAsync("ArgumentNullException in getSimilarDemons: " + e.Message);

                }
                //Console.WriteLine("LevDistance between : " + demon.Name + " and " + searchedDemon + levDistance);

                //If only off by levDist characters, add to List
                if (levDistance <= levDist)
                {
                    simSkills.Add(skill.Name);
                }
            }

            return simSkills;
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nSkill Commands:" +
            "\n* " + MainCommand + " [Skill Name] - Search's for a skill with the name you provided as [Skill Name]. If nothing is found you will recieve a message back stating Skill was not found.";
        }

        #endregion

        #region Private Methods

        //Loads our Skill from a DataGridRow
        private static Skill LoadSkill(DataRow row)
        {
            var name = row["Name"] is DBNull ? "" : (string)row["Name"];

            var skill = new Skill
            {
                Name = name,
                Element = row["Element"] is DBNull ? "" : (string)row["Element"],
                Cost = row["Cost"] is DBNull ? "" : (string)row["Cost"],
                Description = row["Description"] is DBNull ? "" : (string)row["Description"],
                Target = row["Target"] is DBNull ? "" : (string)row["Target"],
                Sp = row["Skill Points"] is DBNull ? "" : (string)row["Skill Points"],
                ExtractExclusive = row["ExtractExclusive"] != null ? false : (bool)row["ExtractExclusive"],
                DuelExclusive = row["DuelExclusive"] != null ? false : (bool)row["DuelExclusive"],
                ExtractTransfer = row["ExtractTransfer"] != null ? false : (bool)row["ExtractTransfer"]
            };
            
            skill.BuildSKill(DemonRetriever.GetDemonsWithSkill(name));

            return skill;
        }

        #endregion
    }
    #region Structs

    //Struct to hold our Skill Data
    public struct Skill
    {
        public string Name;
        public string Element;
        public string Cost;
        public string Description;
        public string Levels;
        public string Target;
        public string Sp;
        public string LearnedBy;
        public string TransferableFrom;
        public bool ExtractExclusive;
        public bool DuelExclusive;
        public bool ExtractTransfer;
        public string TransferrableFrom;        

        public Embed WriteToDiscord()
        {
            //Perform some fixes on values before exporting

            Name = DemonRetriever.FixSkillsNamedAsDemons(Name);
            Element = char.ToUpper(Element[0]) + Element.Substring(1);

            if (Sp == "")
                Sp = "-";

            Description = Description.Replace("\\n", "\n") + TransferrableFrom;
            Levels = Levels.Replace("\\n", "\n");

            var url = "https://dx2wiki.com/index.php/" + Uri.EscapeDataString(Name);
            var thumbnail = "https://teambuilder.dx2wiki.com/Images/Spells/" + Uri.EscapeDataString(Element) + ".png";

            //Generate our embeded message and return it
            var eb = new EmbedBuilder();
            eb.WithTitle(Name);
            eb.AddField("Element: ", Element, true);
            eb.AddField("Cost: ", Cost, true);
            eb.AddField("Target: ", Target, true);
            eb.AddField("Sp: ", Sp, true);
            eb.WithDescription(Description + "\n");
            eb.WithUrl(url);
            eb.WithThumbnailUrl(thumbnail);
            return eb.Build();
        }

        //Builds out our skill with additional details that require some processing
        public void BuildSKill(Dictionary<string, List<Demon>> skillInfos)
        {
            var transferrableFrom = "";

            if (skillInfos["Transferrable"].Count > 0)
            {
                transferrableFrom += "\n\n Transferrable From: ";

                foreach(var s in skillInfos["Transferrable"])
                    transferrableFrom += s.Name + ", ";

                transferrableFrom = transferrableFrom.Remove(transferrableFrom.Length-2, 2);
            }

            TransferrableFrom = transferrableFrom;
        }
    }

    #endregion
}
