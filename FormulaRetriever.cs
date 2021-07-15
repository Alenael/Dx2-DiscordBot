using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dx2_DiscordBot
{
    public class FormulaRetriever : RetrieverBase
    {
        #region Properties

        private string DamageFormula { get; set; }
        private string AccFormula { get; set; }
        private string CounterFormula { get; set; }
        private string SpeedFormula { get; set; }
        private string BuffFormula { get; set; }
        private string InflictionFormula { get; set; }
        private string StatFormula { get; set; }

        private string CritFormula { get; set; }
        private string HealFormula { get; set; }

        #endregion

        #region Constructor

        //Constructor
        public FormulaRetriever(DiscordSocketClient client) : base(client)
        {
            MainCommand = "!dx2formula";
        }

        #endregion

        #region Overrides

        //Initialization
        public async override Task ReadyAsync()
        {
            DamageFormula = "```" +
                "DAMAGE FORMULA = (ATK * 1 - DEF * 0.5) * 0.4  * (SKILL POWER * (1 + % SKILL DAMAGE MODIFIERS FROM SKILL LEVELS) / 100) * (1 + % DAMAGE MODIFIERS) * (1 + TARUNDA/TARUKAJA - RAKUNDA/RAKUKAJA) * (CHARGE / CONCENTRATE: 2.25 or 2.75 w/ Templar Dragon) * (CRIT/WEAKNESS/RESIST)\n" +
                "10 % variance in final damage.\n" +
                "Critical hit multiplier is 1.5x\n" +
                "Weakness hit multiplier is 1.5x\n" +
                "Resistance hit multiplier is 0.7x" +
                "```";

            AccFormula = "```" +
                "TEMP ACCURACY = BASE SKILL ACCURACY * (FUNCTION1(USER AGI - ENEMY AGI) + FUNCTION2(USER LUCK - ENEMY LUCK)) * (1 * SUKUNDA/SUKUKAJA)\n" +
                "FINAL ACCURACY = TEMP ACCURACY + ACC BRANDS + ACC SKILLS - EVASION BRANDS - EVASION SKILLS\n" +
                "MINIMUM ACCURACY = 100 * BASE SKILL ACCURACY * 0.2\n" +
                "There is a minimum hit chance, and it will take either MINIMUM ACCURACY or FINAL ACCURACY depending on what's higher.\n" +
                "FUNCTION1(DIFF_AGI) is:\n" +
                "102 if DIFF_AGI >= 256\n" +
                "100 if DIFF_AGI >= 40\n" +
                "98 if DIFF_AGI >= 30\n" +
                "96 if DIFF_AGI >= 20\n" +
                "94 if DIFF_AGI >= 10\n" +
                "92 if DIFF_AGI >= 0\n" +
                "88 if DIFF_AGI >= -20\n" +
                "84 if DIFF_AGI >= -40\n" +
                "80 if DIFF_AGI >= -60\n" +
                "76 if DIFF_AGI < -60\n" +
                "FUNCTION2(DIFF_LUK) is:\n" +
                "13 if DIFF_LUK >= 256\n" +
                "11 if DIFF_LUK >= 30\n" +
                "9 if DIFF_LUK >= 20\n" +
                "7 if DIFF_LUK >= 10\n" +
                "5 if DIFF_LUK >= 0\n" +
                "0 if DIFF_LUK >= -30\n" +
                "-5 if DIFF_LUK < -30\n" +
                "```";

            CounterFormula = "```" +
                "Counter Chance is rolled sequentially and separately, starting from the highest tier Counter first (Death Counter>Retaliate>Counter) regardless of slot order." +
                "```";

            SpeedFormula = "```" +
                "Party speed is calculated first at an individual demon level then at a party level.\n" +
                "INDIVIDUAL DEMON SPEED = (DEMON AGILITY + SKILL AGILITY + MITAMA AGILITY) *((SKILL SPEED PERCENT +BRAND SPEED PERCENT +PANEL PERCENT) / 100)\n" +
                "PARTY SPEED TOTAL = INDIVIDUAL DEMON SPEED TOTAL / PARTY COUNT(It's an average)\n" +
                "```";

            CritFormula = "```" +
                "LUCK DIFF = USER LUCK - ENEMY LUCK\n" +
                "CRIT LUK VALUE = 20 if LUCK DIFF >= 30,\n" +
                "     15 if LUCK DIFF >= 20,\n" +
                "     10 if LUCK DIFF >= 10,\n" +
                "      0 if LUCK DIFF >= 0,\n" +
                "    -10 if LUCK DIFF < 0\n" +
                "CRIT CHANCE = CRIT LUK VALUE +BASE SKILL CRIT CHANCE + PASSIVE SKILLS / PANEL CRIT CHANCE +CRIT BRANDS - ENEMY CRIT REDUC SKILLS / PANEL + Dx2 CRIT SKILL\n" +
                "```";

            BuffFormula = "```" +
                "-kaja/-kunda effects (applied at end of formula)\n" +
                "Rakukaja: -0.17 = 0.83x damage taken\n" +
                "Tarunda: -0.2 = 0.8x damage taken\n" +
                "Rakukaja + Tarunda: -0.37 = 0.64x damage taken\n" +
                "Tarukaja: +1.2 = 1.2x damage dealt\n" +
                "Rakunda: +1.25 = 1.25x damage dealt\n" +
                "Tarukaja + Rakunda: 1.45 = 1.45x damage dealt\n" +
                "```";

            InflictionFormula = "```" +
                "USER INFLICTION BOOST = AILMENT INFLICT PASSIVES + BRAND AILMENT INFLICT + AILMENT INFLICT FROM SKILL LEVELS + AILMENT INFLICT PANELS\n" +
                "ENEMY AILMENT RESISTS = AILMENT RESIST PASSIVES +BRAND AILMENT RESISTS +AILMENT RESISTS PANELS +Dx2 AILMENT RESISTS SKILLS + CURRENT AILMENT RESIST BUFFS\n" +
                "MINIMUM AILMENT CHANCE = floor(SKILL AILMENT RATE * 0.3)\n" +
                "AILMENT CHANCE = floor(SKILL AILMENT RATE + ((USER LUCK - ENEMY LUCK) * 0.3)) +USER INFLICTION BOOST -ENEMY AILMENT RESISTS\n" +
                "Some demons may have some innate ailment resistances(likely PvE only), modifying the above AILMENT CHANCE:\n" +
                "AILMENT CHANCE = floor(AILMENT CHANCE * ((100 - DEMON AILMENT RESISTS) / 100))\n" +
                "It will use the greater of either MINIMUM AILMENT CHANCE or AILMENT CHANCE to determine the chance of an ailment being successful.\n" +
                "```";

            StatFormula = "```" +
                "HP = VIT * 4.7 + LVL * 7.4\n" +
                "PATK = STR * 2.1 + LVL * 5.6 + 50\n" +
                "MATK = MAG * 2.1 + LVL * 5.6 + 50\n" +
                "PDEF = VIT * 1.1 + STR * 0.5 + LVL * 5.6 + 50\n" +
                "MDEF = VIT * 1.1 + MAG * 0.5 + LVL * 5.6 + 50\n" +
                "```";

            HealFormula = "```" +
                "HEALING FORMULA = (MATK * (HEAL POWER * (1 + % HEALING MODIFIERS FROM SKILL LEVELS) / 100) * HEALING_CONST + MIN_HEAL_VAL) * RANDOM_HEA_VAR * (1 + % HEAL MODIFIERS)" +
                "```";
        }


        //Recieve Messages here
        public async override Task MessageReceivedAsync(SocketMessage message, string serverName, ulong channelId)
        {
            //Let Base Update
            await base.MessageReceivedAsync(message, serverName, channelId);

            if (message.Content.StartsWith(MainCommand))
            {
                if (_client.GetChannel(channelId) is IMessageChannel chnl)
                {
                    var items = message.Content.Split(MainCommand);
                                        
                    switch (items[1].Trim())
                    {
                        case "":
                            await chnl.SendMessageAsync(DamageFormula, false);
                            break;
                        case "acc":
                            await chnl.SendMessageAsync(AccFormula, false);
                            break;
                        case "counter":
                            await chnl.SendMessageAsync(CounterFormula, false);
                            break;
                        case "speed":
                            await chnl.SendMessageAsync(SpeedFormula, false);
                            break;
                        case "buff":
                            await chnl.SendMessageAsync(BuffFormula, false);
                            break;
                        case "inf":
                            await chnl.SendMessageAsync(InflictionFormula, false);
                            break;
                        case "stat":
                            await chnl.SendMessageAsync(StatFormula, false);
                            break;
                        case "heal":
                            await chnl.SendMessageAsync(HealFormula, false);
                            break;
                        case "crit":
                            await chnl.SendMessageAsync(CritFormula, false);
                            break;
                    }
                }
            }
        }

        //Returns the commands for this Retriever
        public override string GetCommands()
        {
            return "\n\nTier Data Commands:" +
            "\n* " + MainCommand + " - Displays standard Damage Formula." +
            "\n* " + MainCommand + "acc - Displays standard Accuracy Formula." +
            "\n* " + MainCommand + "counter - Displays Counter Formula." +
            "\n* " + MainCommand + "speed - Displays Speed Formula." +
            "\n* " + MainCommand + "buff - Displays Buff Formula." +
            "\n* " + MainCommand + "inf - Displays Infliction Formula." +
            "\n* " + MainCommand + "stat - Displays Stat Formulas." +
            "\n* " + MainCommand + "heal - Displays Heal Formula." +
            "\n* " + MainCommand + "crit - Displays Crit Chance Formula.";
        }

        #endregion
    }
}
