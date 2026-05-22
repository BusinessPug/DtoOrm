using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace DtoOrm.Core;

public sealed class ReflectionRowMapper : IRowMapper
{
    public static readonly ReflectionRowMapper Instance = new();

    private readonly ConcurrentDictionary<Type, TypeMap> _maps = new();

    private ReflectionRowMapper()
    {
    }

    public TDto Map<TDto>(DbDataReader reader)
    {
        var map = _maps.GetOrAdd(typeof(TDto), TypeMap.Create);
        return (TDto)map.Map(reader);
    }

    private sealed class TypeMap
    {
        private readonly Type _type;
        private readonly ConstructorInfo? _preferredConstructor;
        private readonly PropertyInfo[] _settableProperties;

        private TypeMap(Type type, ConstructorInfo? preferredConstructor, PropertyInfo[] settableProperties)
        {
            _type = type;
            _preferredConstructor = preferredConstructor;
            _settableProperties = settableProperties;
        }

        public static TypeMap Create(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            var preferred = constructors.FirstOrDefault(c => c.GetParameters().Length > 0)
                            ?? constructors.FirstOrDefault(c => c.GetParameters().Length == 0);

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is not null)
                .ToArray();

            return new TypeMap(type, preferred, props);
        }

        public object Map(DbDataReader reader)
        {
            if (_type == typeof(object))
            {
                throw new InvalidOperationException("Mapping to object is ambiguous. Use a DTO/record type.");
            }

            var fields = ReadFields(reader);

            if (_preferredConstructor is not null && _preferredConstructor.GetParameters().Length > 0)
            {
                return MapUsingConstructor(_preferredConstructor, fields);
            }

            var instance = Activator.CreateInstance(_type)
                           ?? throw new InvalidOperationException($"Could not create instance of '{_type.FullName}'.");

            foreach (var property in _settableProperties)
            {
                if (!fields.TryGetValue(property.Name, out var value))
                {
                    continue;
                }

                property.SetValue(instance, ConvertValue(value, property.PropertyType));
            }

            return instance;
        }

        private static Dictionary<string, FieldValue> ReadFields(DbDataReader reader)
        {
            var fields = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                fields[name] = new FieldValue(i, name, value);
            }

            return fields;
        }

        private static object MapUsingConstructor(ConstructorInfo constructor, Dictionary<string, FieldValue> fields)
        {
            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];

            var allByName = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];

                if (!fields.TryGetValue(p.Name ?? string.Empty, out var field))
                {
                    allByName = false;
                    break;
                }

                args[i] = ConvertValue(field.Value, p.ParameterType);
            }

            if (!allByName)
            {
                if (parameters.Length != fields.Count)
                {
                    throw new InvalidOperationException(
                        $"Could not map selected columns [{string.Join(", ", fields.Keys)}] to constructor '{constructor.DeclaringType?.FullName}'. " +
                        "Selected aliases should match constructor parameter names.");
                }

                var byOrdinal = fields.Values.OrderBy(f => f.Ordinal).ToArray();
                for (var i = 0; i < parameters.Length; i++)
                {
                    args[i] = ConvertValue(byOrdinal[i].Value, parameters[i].ParameterType);
                }
            }

            return constructor.Invoke(args);
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value is null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
                {
                    return null;
                }

                throw new InvalidOperationException($"Cannot assign database NULL to non-nullable '{targetType.FullName}'.");
            }

            var actualTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (actualTarget.IsInstanceOfType(value))
            {
                return value;
            }

            if (actualTarget.IsEnum)
            {
                return value is string text
                    ? Enum.Parse(actualTarget, text, ignoreCase: true)
                    : Enum.ToObject(actualTarget, value);
            }

            if (actualTarget == typeof(Guid))
            {
                return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            }

            if (actualTarget == typeof(DateOnly))
            {
                return value switch
                {
                    DateOnly dateOnly => dateOnly,
                    DateTime dateTime => DateOnly.FromDateTime(dateTime),
                    string text => DateOnly.Parse(text, CultureInfo.InvariantCulture),
                    _ => throw new InvalidOperationException($"Cannot convert '{value.GetType().FullName}' to DateOnly.")
                };
            }

            if (actualTarget == typeof(TimeOnly))
            {
                return value switch
                {
                    TimeOnly timeOnly => timeOnly,
                    TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
                    string text => TimeOnly.Parse(text, CultureInfo.InvariantCulture),
                    _ => throw new InvalidOperationException($"Cannot convert '{value.GetType().FullName}' to TimeOnly.")
                };
            }

            return Convert.ChangeType(value, actualTarget, CultureInfo.InvariantCulture);
        }

        private sealed record FieldValue(int Ordinal, string Name, object? Value);
    }
}
