using System.Linq.Expressions;

namespace JsonRuleEngine.Net
{
    public class PropertyAccessorContext
    {
        public PropertyAccessorContext()
        {
        }

        /// <summary>
        /// Object value compared
        /// </summary>
        public object ValueCompared { get; set; }

        /// <summary>
        /// Name of the prop invoked
        /// </summary>
        public string MemberName { get; set; }

        /// <summary>
        /// Current expression
        /// </summary>
        public Expression Expression { get; set; }
        public Expression InputParam { get; internal set; }
    }
}