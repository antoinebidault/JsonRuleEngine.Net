using System;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// The compare operator used in rule
    /// </summary>
    public enum ConditionRuleOperator
    {
        /// <summary>
        /// Equal
        /// </summary>
        equal,
        /// <summary>
        /// Not equal
        /// </summary>
        notEqual,
        /// <summary>
        /// Less than
        /// </summary>
        lessThan,
        /// <summary>
        /// Less than inclusive 
        /// </summary>
        lessThanInclusive,
        /// <summary>
        /// Greater than 
        /// </summary>
        greaterThan,
        /// <summary>
        /// Greater than inclmusive
        /// </summary>
        greaterThanInclusive,
        /// <summary>
        /// In (requires an array of string in value)
        /// </summary>
        @in,
        /// <summary>
        /// Not in Not in ('test', 'test2') (requires an array of string in value)
        /// </summary>
        notIn,
        /// <summary>
        /// Contains (in string)
        /// </summary>
        contains,
        /// <summary>
        /// Dos not contains (in string)
        /// </summary>
        doesNotContains,
        /// <summary>
        /// Is null
        /// </summary>
        isNull,
        /// <summary>
        /// Is not null
        /// </summary>
        isNotNull,
        /// <summary>
        /// Is empty
        /// </summary>
        isEmpty,
        isNotEmpty
    }
}
