using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        [Fact]
        public void ValidateExpression()
        {
            var data = File.ReadAllText(Path.Combine("TestJsons/", "complex.json"));
            var items = FakeGameService.GetData();
            var whiteList = new List<string>() {
                "Category",
                "Price",
                "Name",
                "Editor.Name",
                "Reviews.Id"
            };
            var result = new JsonRuleEngine().ValidateExpressionFields(data, whiteList);
            Assert.True(result.Success);
        }

    }
}
