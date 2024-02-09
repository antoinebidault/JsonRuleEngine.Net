using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace JsonRuleEngine.Net.Models
{
    public class EvaluateOptions<TInput>
    {

        private IDictionary<string, Expression> _transformers =
            new Dictionary<string, Expression>();

        internal Expression GetTransformer<TInput>(string field, ParameterExpression parameter)
        {

            var transformer = _transformers[field];
            return transformer;
        }

        internal bool HasTransformer(string field)
        {
            return _transformers.ContainsKey(field);
        }

        public EvaluateOptions<TInput> ForProperty<TOut>(string propertyName, Expression<Func<TInput, TOut>> call)
        {
            var param = call.Parameters[0];
            Expression outerMember = call.Body;
            _transformers.Add(propertyName, outerMember);
            return this;
        }

    }
}
