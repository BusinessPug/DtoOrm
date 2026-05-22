using System.Text;

namespace DtoOrm.MariaDb.Schema;

public static class SchemaName
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
        "continue","decimal","default","delegate","do","double","else","enum","event","explicit",
        "extern","false","finally","fixed","float","for","foreach","goto","if","implicit","in",
        "int","interface","internal","is","lock","long","namespace","new","null","object","operator",
        "out","override","params","private","protected","public","readonly","ref","return","sbyte",
        "sealed","short","sizeof","stackalloc","static","string","struct","switch","this","throw",
        "true","try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void",
        "volatile","while","record","required","file"
    };

    public static string ToPascalCaseIdentifier(string dbName, string fallbackPrefix = "X")
    {
        if (string.IsNullOrWhiteSpace(dbName))
        {
            return fallbackPrefix;
        }

        var builder = new StringBuilder();
        var capitalizeNext = true;

        foreach (var ch in dbName)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                capitalizeNext = true;
                continue;
            }

            if (builder.Length == 0 && char.IsDigit(ch))
            {
                builder.Append(fallbackPrefix);
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
            capitalizeNext = false;
        }

        if (builder.Length == 0)
        {
            builder.Append(fallbackPrefix);
        }

        var result = builder.ToString();

        if (Keywords.Contains(result) || Keywords.Contains(char.ToLowerInvariant(result[0]) + result[1..]))
        {
            result += "_";
        }

        return result;
    }

    public static string ToAlias(string tableName)
    {
        var letters = new string(tableName.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(letters) ? "t" : letters;
    }
}
