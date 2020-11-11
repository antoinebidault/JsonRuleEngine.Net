using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public class BaseTests
    {
        [Fact]
        public void ConditionTest()
        {
            string rules = File.ReadAllText("rules.json");

            var expression = JsonRuleEngine.ParseExpression<Game>(rules);

            var datas = FakeGameService.GetDatas();

            var list = datas.Where(expression).ToList();

            Assert.True(list.Count > 0);
        }
    }
}
