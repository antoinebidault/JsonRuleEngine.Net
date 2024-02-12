using System.Linq.Expressions;

namespace JsonRuleEngine.Net.Models
{
    public class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _newParameter;

        public ParameterReplaceVisitor(ParameterExpression newParameter)
        {
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _newParameter;
        }
    }
}
