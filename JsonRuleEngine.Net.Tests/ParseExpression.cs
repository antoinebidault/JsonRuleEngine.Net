using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        [Fact()]
        public void ParseExpressionTest()
        {
            var expression = JsonRuleEngine.ParseExpression<Game>(new ConditionRuleSet() { Field = "Name", Operator = ConditionRuleOperator.equal, Value = "GTA" });
            var gameToTest = new Game() { Name = "GTA" };
            Assert.True(expression.Compile().Invoke(gameToTest));
        }
    }
}
