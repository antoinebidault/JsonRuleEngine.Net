using System;
using System.Linq.Expressions;

namespace JsonRuleEngine.Net
{
    /// <summary>
    /// Context provided to <see cref="JsonRuleEngine.CustomConditionRuleSetAccessor"/>.
    /// Unlike <see cref="PropertyAccessorContext"/>, which is invoked once per field segment,
    /// this context describes a complete leaf <see cref="ConditionRuleSet"/> : the full
    /// (non splitted) field path, its operator and its value.
    /// </summary>
    public class ConditionRuleSetAccessorContext
    {
        /// <summary>
        /// The context is instanciated by the engine before each accessor call to get the necessary data
        /// </summary>
        public ConditionRuleSetAccessorContext()
        {
        }

        /// <summary>
        /// The whole leaf rule, with its original dotted field, operator and value
        /// </summary>
        public ConditionRuleSet Rule { get; internal set; }

        /// <summary>
        /// Shortcut on Rule.Field. The complete dotted path, never splitted on the separator
        /// </summary>
        public string Field
        {
            get { return Rule == null ? null : Rule.Field; }
        }

        /// <summary>
        /// Operator of the current rule
        /// </summary>
        public ConditionRuleOperator Operator
        {
            get { return Rule == null ? ConditionRuleOperator.equal : Rule.Operator; }
        }

        /// <summary>
        /// Object value compared. Pre-filled from Rule.Value.
        /// Setting it changes the value used by <see cref="ApplyOperator(Expression)"/>
        /// and by the automatic operator application
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// The expression the returned expression must be built on :
        /// the parameter of T for a top level rule, or the collection item parameter
        /// when the rule comes from a collection sub rule (see <see cref="IsCollectionItem"/>)
        /// </summary>
        public ParameterExpression InputParam { get; internal set; }

        /// <summary>
        /// Type of <see cref="InputParam"/>
        /// </summary>
        public Type InputType
        {
            get { return InputParam == null ? null : InputParam.Type; }
        }

        /// <summary>
        /// True when the rule is a sub rule of a collection (ex : Reviews.Text),
        /// in that case InputParam is the collection item parameter and the engine
        /// still wraps the result in a Any() / All() call
        /// </summary>
        public bool IsCollectionItem { get; internal set; }

        /// <summary>
        /// The engine operation factory, used by <see cref="ApplyOperator(Expression)"/>
        /// </summary>
        internal Func<Expression, object, Expression> OperatorFactory { get; set; }

        /// <summary>
        /// Apply the engine operator logic (in/notIn, dates, TimeSpan, nullables, ...)
        /// on the provided member access, using <see cref="Value"/> as compared value
        /// </summary>
        /// <param name="memberAccess"></param>
        /// <returns>A boolean expression</returns>
        public Expression ApplyOperator(Expression memberAccess)
        {
            return ApplyOperator(memberAccess, Value);
        }

        /// <summary>
        /// Apply the engine operator logic (in/notIn, dates, TimeSpan, nullables, ...)
        /// on the provided member access, using the provided compared value
        /// </summary>
        /// <param name="memberAccess"></param>
        /// <param name="value"></param>
        /// <returns>A boolean expression</returns>
        public Expression ApplyOperator(Expression memberAccess, object value)
        {
            if (OperatorFactory == null)
            {
                throw new JsonRuleEngineException(JsonRuleEngineExceptionCategory.UnknownError,
                    "The context is not bound to an engine instance");
            }

            return OperatorFactory(memberAccess, value);
        }
    }
}
