using System;
using System.Collections.Generic;

namespace JsonRuleEngine.Net
{

    public class ConditionRuleSet
    {
        /// <summary>
        /// Separator
        /// </summary>
        public ConditionRuleSeparator? Separator { get; set; }

        /// <summary>
        /// Operator
        /// </summary>
        public ConditionRuleOperator Operator { get; set; }

        /// <summary>
        /// The field name evaluated
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The value to match
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<ConditionRuleSet> Rules { get;  set; }
    }
}
