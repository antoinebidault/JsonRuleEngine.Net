using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public class Game
    {
        public string Name { get; set; }
        public double Price { get; set; }

        public string Category { get; set; } = "Action";
    }

    public static class FakeGameService
    {
        public static IQueryable<Game> GetDatas()
        {
            return new List<Game>()
            {
                new Game()
                {
                    Name= "Assassin's creed",
                    Price = 45.3
                },
                 new Game()
                {
                    Name= "Destiny",
                    Price = 23.3
                },
                 new Game()
                {
                    Name= "The forest",
                    Price = 22
                },
                 new Game()
                {
                    Name= "Lowe",
                    Price = 13
                },
                 new Game()
                {
                    Name= "GTA V",
                    Price = 77
                },
                 new Game()
                {
                    Name= "GTA IV",
                    Price = 24
                }
            }.AsQueryable();
        }
    }
}
