using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JsonRuleEngine.Net
{
    internal static class TypeExtensions
    {
        internal static bool IsNullable(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }
        internal static bool IsStringLong(this object value)
        {
            if (value is string)
            {
                if (long.TryParse(value.ToString(), out long result))
                    return true;
            }

            return false;
        }

        internal static bool IsStringDouble(this object value)
        {
            if (value is string)
            {
                if (double.TryParse(value.ToString(),NumberStyles.Number, CultureInfo.CreateSpecificCulture("en-US"), out double result))
                    return true;
            }

            return false;
        }

        internal static bool IsArray(this Type type)
        {
            return( type != typeof(string) &&
                    type.IsGenericType &&
                    !type.IsDictionary() &&
                    typeof(IEnumerable).IsAssignableFrom(type)) || type == typeof(JArray);
        }

        public static bool IsDictionary(this Type type)
        {
            return (type.GetGenericTypeDefinition().IsAssignableFrom(typeof(IDictionary<,>)) || type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)));
        }

        public static bool IsNullableEnum(this Type t)
        {
            return t.IsGenericType &&
                   t.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                   t.GetGenericArguments()[0].IsEnum;
        }

        internal static object GetValue(this Type type, object value)
        {
            try
            {
                if (type == typeof(DateTime?))
                {
                    DateTime? output = null;
                    if (value != null && value.ToString() != "")
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


                if (type.IsEnum || type.IsNullableEnum())
                {
                    if (value == null)
                    {
                        return null;
                    }

                    return Enum.Parse(type, value.ToString());
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
