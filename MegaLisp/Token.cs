namespace MegaLisp;

public class Token
{
    public string Data;
    public Token Car;
    public Token Cons;
    public bool IsSymbol => Car == null && IsString == false;
    public bool IsString => Data?.StartsWith("\"") ?? false;

    public string ThisString()
    {
        if (Car != null)
        {
            return "(" + Car + ")";
        }

        return Data;
    }

    public override string ToString()
    {
        if (Cons != null) return ThisString() + " " + Cons;
        return ThisString();
    }
}