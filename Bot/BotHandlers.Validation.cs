namespace Bot;

public sealed partial class BotHandlers
{
    internal static bool TryNormalizeLanguage(string input, out string lang)
    {
        lang = input.Trim().ToLowerInvariant();
        if (lang is "fi" or "en")
        {
            return true;
        }

        lang = string.Empty;
        return false;
    }

    internal static bool LooksLikeRealName(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var p in parts)
        {
            if (p.Length < 2)
            {
                return false;
            }

            foreach (var ch in p)
            {
                if (!(char.IsLetter(ch) || ch is '-' or '\''))
                {
                    return false;
                }
            }
        }
        return true;
    }

    internal static string NormalizeName(string input)
    {
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<string>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.Length == 0)
            {
                continue;
            }
            var chars = token.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetter(chars[i]))
                {
                    if (i == 0 || chars[i - 1] == '-')
                    {
                        chars[i] = char.ToUpperInvariant(chars[i]);
                    }
                    else
                    {
                        chars[i] = char.ToLowerInvariant(chars[i]);
                    }
                }
            }
            result.Add(new string(chars));
        }
        return string.Join(' ', result);
    }

    internal static bool TryNormalizeHandle(string input, out string handle)
    {
        handle = input.Trim();
        if (handle.StartsWith("@"))
        {
            handle = handle[1..];
        }

        handle = handle.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(handle))
        {
            return false;
        }

        if (handle.Length < 5 || handle.Length > 32)
        {
            return false;
        }

        if (!handle.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
        {
            return false;
        }

        return true;
    }

    internal static bool ParseYesNo(string lang, string input, out bool yes)
    {
        var v = input.Trim().ToLowerInvariant();
        if (lang == "fi")
        {
            if (v is "kyllä" or "kylla" or "k" or "joo" or "yes" or "y") { yes = true; return true; }
            if (v is "ei" or "e" or "no" or "n") { yes = false; return true; }
        }
        else
        {
            if (v is "yes" or "y") { yes = true; return true; }
            if (v is "no" or "n") { yes = false; return true; }
        }
        yes = false;
        return false;
    }

    internal static bool IsCancelAvec(string lang, string input)
    {
        var v = input.Trim().ToLowerInvariant();
        if (lang == "fi")
        {
            return v is "ei" or "peru" or "peruuta" or "poista";
        }

        return v is "none" or "no" or "cancel" or "remove";
    }
}
