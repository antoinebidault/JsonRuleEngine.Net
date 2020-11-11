using System;

namespace JsonRuleEngine.Net
{
    public enum ConditionRuleOperator
    {
        equal,
        notEqual,
        lessThan,
        lessThanInclusive,
        greaterThan,
        greaterThanInclusive,
        @in,
        notIn,
        contains,
        doesNotContains
    }
}
