using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{

    public static class FakeGameService
    {
        public static IQueryable<Game> GetDatas()
        {
            return new List<Game>()
            {
                new Game()
                {
                    Name= "Assassin's creed",
                    Price = 45.3,
                    BoolValue = true,
                    Date =new DateTime(2021,1,1)
                },
                 new Game()
                {
                    Name= "Destiny",
                    Price = 23.3,
                    Date =new DateTime(2021,1,12)
                },
                 new Game()
                {
                    Name= "The forest",
                    Price = 22,
                    Date =new DateTime(2021,1,11)
                },
                 new Game()
                {
                    Name= "Lowe",
                    Price = 13,
                    Date =new DateTime(2021,1,2)
                },
                 new Game()
                {
                    Name= "GTA V",
                    Price = 77,
                    Date =new DateTime(2022,1,1)
                },
                 new Game()
                {
                    Name= "GTA IV",
                    Price = 24,
                    Date =new DateTime(2018,1,1)
                }
            }.AsQueryable();
        }
    }
}
