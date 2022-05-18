using System;

namespace JsonRuleEngine.Net
{
    public class ReturnValue<T>
    {
        public Type Type { get; set; }

        public T Value { get; set; }
    }
}