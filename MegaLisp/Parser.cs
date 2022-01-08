namespace MegaLisp;

public class Parser
{
    public ReadOnlySpan<char> TokenizeExpression(ReadOnlySpan<char> code, out Token result)
    {
        code = Tokenize(code, out result);
        if (result != null)
        {
            return TokenizeExpression(code, out result.Cons);
        }

        return code;
    }

    public ReadOnlySpan<char> Tokenize(ReadOnlySpan<char> code, out Token result)
    {
        code = code.SkipSpaces();
        if (code.Length == 0 || code.StartsWith(".") || code.StartsWith("\n"))
        {
            result = null;
            return code.Next();
        }

        if (code.StartsWith("("))
        {
            code = code.Next();
            result = new Token();
            return TokenizeExpression(code, out result.Car);
        }

        if (code.StartsWith(")"))
        {
            result = null;
            return code.Next();
        }

        if (code.StartsWith("\""))
        {
            var start = code;
            code = code.Next();
            while (true)
            {
                if (code.Length == 0)
                    throw new Exception("Parser error while reading string");
                if (code.StartsWith("\"\""))
                {
                    code = code.Next(2);
                    continue;
                }

                if (code.StartsWith("\""))
                {
                    code = code.Next();
                    break;
                }

                code = code.Next(1);
            }

            var str = start.Slice(0, start.Length - code.Length);
            result = new Token() {Data = str.ToString()};
            return code.Next();
        }
        else
        {
            var start = code;
            code = code.SkipWhile(x => (!char.IsWhiteSpace(x)) && x != ')');
            var str = start.Slice(0, start.Length - code.Length);
            result = new Token {Data = str.ToString()};
            return code;
        }
    }
}

static class ParserExtensions
{
    public static ReadOnlySpan<char> SkipWhile(this ReadOnlySpan<char> code, Func<char, bool> expr)
    {
        var len = code.Length;
        int i = 0;
        for (; i < len; i++)
        {
            if (expr(code[i]) == false)
                break;
        }

        return code.Slice(i);
    }

    public static ReadOnlySpan<char> SkipWhiteSpace(this ReadOnlySpan<char> code) =>
        code.SkipWhile(x => char.IsWhiteSpace(x));

    public static ReadOnlySpan<char> SkipSpaces(this ReadOnlySpan<char> code) =>
        code.SkipWhile(x => char.IsWhiteSpace(x) && x != '\n');

    public static ReadOnlySpan<char> Next(this ReadOnlySpan<char> code, int cnt = 1) =>
        code.Length < cnt ? code : code.Slice(cnt);
}