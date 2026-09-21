namespace SanchesTV.Core.Search;

public static class FuzzyMatcher
{
    public static bool IsMatch(string query, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var q = Normalize(query);
        if (q.Length == 0)
            return true;

        foreach (var value in values)
        {
            var candidate = Normalize(value);
            if (candidate.Length == 0)
                continue;

            if (candidate.Contains(q, StringComparison.Ordinal))
                return true;

            foreach (var token in candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith(q, StringComparison.Ordinal))
                    return true;

                if (q.Length >= 3)
                {
                    var max = q.Length <= 5 ? 1 : q.Length <= 9 ? 2 : 3;
                    if (Math.Abs(token.Length - q.Length) <= max &&
                        Distance(q, token) <= max)
                        return true;
                }
            }
        }

        return false;
    }

    public static int Distance(string left, string right)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var rows = left.Length + 1;
        var cols = right.Length + 1;
        var matrix = new int[rows, cols];

        for (var i = 0; i < rows; i++)
            matrix[i, 0] = i;
        for (var j = 0; j < cols; j++)
            matrix[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 &&
                    left[i - 1] == right[j - 2] &&
                    left[i - 2] == right[j - 1])
                {
                    matrix[i, j] = Math.Min(matrix[i, j], matrix[i - 2, j - 2] + 1);
                }
            }
        }

        return matrix[left.Length, right.Length];
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return SanchesTV.Core.Parsing.TextNormalizer.Normalize(value);
    }
}
