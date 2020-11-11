using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public class BaseTests
    {
        [Fact]
        public void Simple()
        {
            List<Game> list = Test("simple.json");

            Assert.True(list.Count == 1);
        }

        [Fact]
        public void InCondition()
        {
            List<Game> list = Test("in.json");

            Assert.True(list.Count == 2);
        }

        [Fact]
        public void GreaterThan()
        {
            List<Game> list = Test("greaterThan.json");
            int total = FakeGameService.GetDatas().Count();

            Assert.True(list.Count > 0 && list.Count < total);
        }

        [Fact]
        public void Complex()
        {
            List<Game> list = Test("complex.json");

            Assert.True(list.Count > 0);
        }

        private static List<Game> Test(string jsonRuleFilePath)
        {
            string rules = File.ReadAllText(jsonRuleFilePath);

            var expression = JsonRuleEngine.ParseExpression<Game>(rules);

            var datas = FakeGameService.GetDatas();
            var list = datas.Where(expression).ToList();
            return list;
        }
    }
}
