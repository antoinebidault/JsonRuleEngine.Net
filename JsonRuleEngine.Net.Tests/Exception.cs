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
        public void WrongJsonFormat()
        {
            var exception = Assert.Throws<JsonRuleEngineException>(() => Test("wrongFormat.json"));
            Assert.Equal(JsonRuleEngineExceptionCategory.InvalidJsonRules, exception.Type);
        }

        [Fact]
        public void WrongField()
        {
            var exception = Assert.Throws<JsonRuleEngineException>(() => Test("wrongField.json"));
            Assert.Equal(JsonRuleEngineExceptionCategory.InvalidField, exception.Type);
        }

    }
}
