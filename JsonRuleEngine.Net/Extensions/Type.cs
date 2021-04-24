using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace JsonRuleEngine.Net
{
    internal static class TypeExtensions
    {
        internal static bool IsNullable(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        internal static bool IsArray(this Type type)
        {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        internal static object GetValue(this Type type, object value)
        {
            try
            {
                if (type == typeof(DateTime?))
                {
                    DateTime? output = null;
                    if (value != null || value.ToString() != "")
                    {
                        output = DateTime.Parse(value.ToString());
                    }

                    return output;
                }

                if (type == typeof(DateTime))
                {
                    return DateTime.Parse(value.ToString());
                }


                if (type == typeof(Guid) || type == typeof(Guid?))
                {
                    return Guid.Parse(value.ToString());
                }

                return Convert.ChangeType(value, type);
            }
            catch
            {
                if (type.IsValueType)
                {
                    return Activator.CreateInstance(type);
                }
                return null;
            }
        }

    }
}
