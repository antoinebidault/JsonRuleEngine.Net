using System;
using System.Collections.Generic;
using System.Text;

namespace JsonRuleEngine.Net
{
    public class JsonRuleEngineException : Exception
    {
        public JsonRuleEngineException(JsonRuleEngineExceptionCategory type, string message):base(message)
        {
            this.Type = type;
        }
        public JsonRuleEngineExceptionCategory Type { get; set; }
    }

    public enum JsonRuleEngineExceptionCategory
    {
        UnknownError,
        InvalidJsonRules,
        InvalidField,
        InvalidValue
    }
}
