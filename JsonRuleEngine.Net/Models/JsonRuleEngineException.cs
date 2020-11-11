using System;
using System.Collections.Generic;
using System.Text;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// Custom exception class with an enum for exception caracterisation
    /// </summary>
    public class JsonRuleEngineException : Exception
    {
        /// <param name="type">Type of the message</param>
        /// <param name="message">Exception message</param>
        public JsonRuleEngineException(JsonRuleEngineExceptionCategory type, string message):base(message)
        {
            this.Type = type;
        }

        /// <summary>
        /// Strong type of the exception
        /// </summary>
        public JsonRuleEngineExceptionCategory Type { get; set; }
    }

    /// <summary>
    /// Category of exception
    /// </summary>
    public enum JsonRuleEngineExceptionCategory
    {
        UnknownError,
        InvalidJsonRules,
        InvalidField,
        InvalidValue
    }
}
