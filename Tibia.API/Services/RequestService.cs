﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Tibia.API.Entities;
using Tibia.API.Entities.Contracts;
using Tibia.API.Enums;
using Tibia.API.Exceptions;
using Tibia.API.Extensions;
using Tibia.API.ObjectValues;
using Tibia.API.Services.Contracts;

namespace Tibia.API.Services
{
    public class RequestService : IRequestService
    {
        private readonly IConvertService _convertService;

        public RequestService (IConvertService convertService)
        {
            _convertService = convertService;
        }

        public IEnumerable<IGuild> GetAllGuilds(string world)
        {
            throw new NotImplementedException();
        }

        public ICharacter GetCharacterInformation(ICharacter character)
        {
            if (character == null)
                throw new ArgumentNullException("character");

            if (String.IsNullOrEmpty(character.Name))
                throw new ArgumentNullException("character.Name");

            return GetCharacterInformation(character.Name);
        }

        public ICharacter GetCharacterInformation(string characterName)
        {
            if (String.IsNullOrEmpty(characterName))
                throw new InvalidDataException("characterName var can't be null or empty.");

            characterName = _convertService.ToCharacterNameLink(characterName);

            var web = new HtmlWeb();
            var document = web.Load(Links.CharacterPage + characterName);

            if (document.DocumentNode.Descendants().Any(x => x.InnerText.Contains((ErrorMessages.InvalidCharacter))))
                throw new InvalidWorldException("Invalid character: " + characterName);

            var character = new Character();

            var characterInformationNodes = document.DocumentNode.SelectNodes("(//*[@id='characters']//div[@class='BoxContent']/table[.//b[contains(text(), 'Character Information')]])//tr[@bgcolor!='#505050']");
            var informatonXpath = "td[1][contains(text(), '{0}')]/../td[2]";
            var informatonXpathAch = "td[1]/nobr[contains(text(), '{0}')]/../../td[2]";
            character.Name = GetHtmlString(characterInformationNodes, String.Format(informatonXpath, "Name:"));
            character.Sex = GetHtmlSex(characterInformationNodes, String.Format(informatonXpath, "Sex:"));
            character.Vocation = GetHtmlVocation(characterInformationNodes, String.Format(informatonXpath, "Vocation:"));
            character.Level = GetHtmlInt(characterInformationNodes, String.Format(informatonXpath, "Level:"));
            character.AchievementPoints = GetHtmlInt(characterInformationNodes, String.Format(informatonXpathAch, "Points:"));
            character.World = GetHtmlString(characterInformationNodes, String.Format(informatonXpath, "World:"));
            character.Residence = GetHtmlString(characterInformationNodes, String.Format(informatonXpath, "Residence:"));
            character.MarriedTo = GetHtmlCharacter(characterInformationNodes, String.Format(informatonXpath, "Married"));
            character.GuildMembership = GetHtmlGuildMembership(characterInformationNodes, String.Format(informatonXpath, "Guild"));
            character.LastLogin = GetHtmlDateTime(characterInformationNodes, String.Format(informatonXpath, "Login:"));
            character.Comment = GetHtmlString(characterInformationNodes, String.Format(informatonXpath, "Comment:"));
            character.AccountStatus = GetHtmlAccountStatus(characterInformationNodes, String.Format(informatonXpath, "Status:"));
            
            var characterAchievementNodes = document.DocumentNode.SelectNodes("(//*[@id='characters']//div[@class='BoxContent']/table[.//b[contains(text(), 'Account Achievements')]])//tr[@bgcolor!='#505050']");
            var achievements = GetHtmlAchievementList(characterAchievementNodes);
            if (achievements != null && achievements.Any())
                character.AddAchievement(achievements);

            var characterDeathsNodes = document.DocumentNode.SelectNodes("(//*[@id='characters']//div[@class='BoxContent']/table[.//b[contains(text(), 'Character Deaths')]])//tr[@bgcolor!='#505050']");
            var deaths = GetHtmlCharacterDeaths(characterDeathsNodes);
            if (deaths != null && deaths.Any())
                character.AddDeath(deaths);

            var characterAccountNodes = document.DocumentNode.SelectNodes("(//*[@id='characters']//div[@class='BoxContent']/table[.//b[contains(text(), 'Account Information')]])//tr[@bgcolor!='#505050']");
            var accountXpath = "td[1][contains(text(), '{0}')]/../td[2]";
            character.LoyalityTitle = GetHtmlString(characterAccountNodes, String.Format(accountXpath, "Title:"));
            character.CreatedDate = GetHtmlDateTime(characterAccountNodes, String.Format(accountXpath, "Created:"));

            //characters list: todo



            return character;
        }

        public IGuild GetGuildInformation(string guildName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ICharacter> GetOnlineCharacters(string world)
        {
            if (String.IsNullOrEmpty(world))
                throw new InvalidDataException("world var can't be null or empty.");

            var web = new HtmlWeb();
            var document = web.Load(Links.WorldPage + world);

            if (document.DocumentNode.Descendants().Any(x => x.InnerText.Contains((ErrorMessages.InvalidWorldName))))
                throw new InvalidWorldException("Invalid world: " + world);

            var nodes = document.DocumentNode.SelectNodes("//*[@id='worlds']//table//tr[@class != 'LabelH']").Where(x => x.Attributes["class"].Value == "Odd" || x.Attributes["class"].Value == "Even");

            var characters = new List<ICharacter>();
            foreach(var node in nodes)
            {
                var character = new Character();
                character.Name = GetHtmlString(node, "td[1]/a[last()]");
                character.Level = GetHtmlInt(node, "td[2]");
                character.Vocation = GetHtmlVocation(node, "td[3]");

                characters.Add(character);
            }

            return characters;
        }

        #region Helpers

        private string GetHtmlString(HtmlNodeCollection nodes, string xpath)
        {
            if (nodes == null || !nodes.Any())
                return String.Empty;

            var xpathNode = nodes.FirstOrDefault(x => x.SelectNodes(xpath) != null);
            if (xpathNode == null) return String.Empty;

            var value = xpathNode.SelectNodes(xpath).First().InnerText;
            if (String.IsNullOrEmpty(value)) return String.Empty;

            return HtmlEntity.DeEntitize(value).Replace(" ", " "); // removes special space
        }

        private string GetHtmlString(HtmlNode node, string xpath)
        {
            if (node == null)
                return String.Empty;

            var nodes = node.SelectNodes(xpath);
            if (nodes == null || !nodes.Any()) return String.Empty;

            var value = nodes.First().InnerText;
            if (String.IsNullOrEmpty(value)) return String.Empty;

            return HtmlEntity.DeEntitize(value).Replace(" ", " "); // removes special space
        }

        private int GetHtmlInt(HtmlNodeCollection node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return Convert.ToInt32(value);
        }

        private int GetHtmlInt(HtmlNode node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return Convert.ToInt32(value);
        }

        private Vocation GetHtmlVocation(HtmlNodeCollection node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return _convertService.ToVocation(value);
        }

        private Vocation GetHtmlVocation(HtmlNode node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return _convertService.ToVocation(value);
        }

        private Sex GetHtmlSex(HtmlNodeCollection node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return _convertService.ToSex(value);
        }

        private DateTime GetHtmlDateTime(HtmlNodeCollection node, string xpath, string format = "MMM dd yyyy, HH:mm:ss CET")
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return DateTime.MinValue;

            if (value == ErrorMessages.NeverLogedIn) return DateTime.MinValue;
            
            var hourDifference = TimeZoneInfo.Local.BaseUtcOffset.Hours;

            if (value.Contains("CEST"))
            {
                format = format.Replace("CET", "CEST");
                hourDifference -= 1;
            }

            var date = DateTime.ParseExact(value, format, new CultureInfo("en-US"));
            return date.AddHours(hourDifference);
        }

        private DateTime GetHtmlDateTime(HtmlNode node, string xpath, string format = "MMM dd yyyy, HH:mm:ss CET")
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return DateTime.MinValue;

            var hourDifference = TimeZoneInfo.Local.BaseUtcOffset.Hours;

            if (value.Contains("CEST"))
            {
                format = format.Replace("CET", "CEST");
                hourDifference -= 1;
            }

            var date = DateTime.ParseExact(value, format, new CultureInfo("en-US"));
            return date.AddHours(hourDifference);
        }

        private AccountStatus GetHtmlAccountStatus(HtmlNodeCollection node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return 0;

            return _convertService.ToAccountStatus(value);
        }

        private IGuildMembership GetHtmlGuildMembership(HtmlNodeCollection node, string xpath)
        {
            var value = GetHtmlString(node, xpath);
            if (String.IsNullOrEmpty(value)) return null;

            var membership = new GuildMembership();
            membership.Ranking = new GuildRanking() { Name = value.Split("of the").First().Trim() };
            membership.Guild = new Guild() { Name = value.Split("of the").Last().Trim() };

            return membership;
        }

        private List<IAchievement> GetHtmlAchievementList(HtmlNodeCollection nodes)
        {
            if (nodes == null || !nodes.Any())
                return null;

            var achievements = new List<IAchievement>();
            foreach (var acvNode in nodes)
            {
                if (acvNode.InnerText == ErrorMessages.NoAchievements)
                    return null;

                var achievement = new Achievement();
                achievement.Rarity = acvNode.SelectNodes("td[1]/img").Count;
                achievement.Name = GetHtmlString(acvNode, "td[2]");
                achievement.IsSecret = acvNode.SelectNodes("td[2]/img") != null;

                achievements.Add(achievement);
            }

            return achievements;
        }

        private List<ICharacterDeath> GetHtmlCharacterDeaths(HtmlNodeCollection nodes)
        {
            if (nodes == null || !nodes.Any()) return null;

            var listDeaths = new List<ICharacterDeath>();
            foreach(var node in nodes)
            {
                var death = new CharacterDeath();
                death.Date = GetHtmlDateTime(node, "td[1]");
                death.Message = GetHtmlString(node, "td[2]");

                listDeaths.Add(death);
            }

            return listDeaths;
        }

        private ICharacter GetHtmlCharacter(HtmlNodeCollection nodes, string xpath)
        {
            var characterName = GetHtmlString(nodes, xpath);
            if (String.IsNullOrEmpty(characterName))
                return null;

            return new Character() { Name = characterName };
        }

        #endregion
    }
}
