using System.Collections.Generic;
using Xunit;

namespace JsonRuleEngine.Net.Tests
{
    public partial class BaseTests
    {
        // https://github.com/antoinebidault/JsonRuleEngine.Net : "in" operator with a scalar
        // dictionary value crashed, while the same rules on a typed object worked
        private const string SdbRules = @"{
          ""separator"": ""And"",
          ""rules"": [
            { ""field"": ""ItemCode"", ""operator"": ""equal"", ""value"": ""BU"" },
            { ""field"": ""Angle"", ""operator"": ""equal"", ""value"": 90 },
            { ""field"": ""DuctConnectionDiameter"", ""operator"": ""in"", ""value"": [125,160,200] }
          ]
        }";

        [Fact]
        public void In_With_Dictionary()
        {
            var attributeValues = new Dictionary<string, object>()
            {
                { "DuctConnectionDiameter", 160 },
                { "ItemCode", "BU" },
                { "Angle", 90 }
            };

            bool result = new JsonRuleEngine().Evaluate(attributeValues, SdbRules);

            Assert.True(result);
        }

        [Fact]
        public void In_With_AnonObject()
        {
            var obj = new
            {
                DuctConnectionDiameter = 160,
                ItemCode = "BU",
                Angle = 90
            };

            bool result = new JsonRuleEngine().Evaluate(obj, SdbRules);

            Assert.True(result);
        }
    }
}
