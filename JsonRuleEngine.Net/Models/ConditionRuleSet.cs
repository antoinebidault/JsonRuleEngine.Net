using System;
using System.Collections.Generic;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// Object used by the JsonRuleEngine for evaluating expressions
    /// </summary>
    public class ConditionRuleSet
    {
        /// <summary>
        /// Separator
        /// </summary>
        public ConditionRuleSeparator? Separator { get; set; } = null;

        /// <summary>
        /// Operator
        /// </summary>
        public ConditionRuleOperator Operator { get; set; } = ConditionRuleOperator.equal;

        /// <summary>
        /// The field name evaluated
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The value to match
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// List of sub rules
        /// </summary>
        public IEnumerable<ConditionRuleSet> Rules { get; set; }

        /// <summary>
        /// Collection rules 
        /// </summary>
        internal IEnumerable<ConditionRuleSet> CollectionRules { get; set; }
        internal Type DictionaryInternalType { get; set; }
    }
}
