using System;
using System.Collections.Generic;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// Object used by the JsonRuleEngine for evaluating expressions
    /// </summary>
    public class ConditionRuleSet<TOut> : ConditionRuleSet
    {
        public ConditionRuleSet()
        {
        }

        public ConditionRuleSet(ConditionRuleSet conditionRuleSet)
        {
            this.Field = conditionRuleSet.Field;
            this.Operator = conditionRuleSet.Operator;
            this.Rules = conditionRuleSet.Rules;
            this.Separator = conditionRuleSet.Separator;
            this.Value = conditionRuleSet.Value;
        }

        /// <summary>
        /// The value that is returned
        /// </summary>
        public ReturnValue<TOut> ReturnValue { get; set; }
    }
}
