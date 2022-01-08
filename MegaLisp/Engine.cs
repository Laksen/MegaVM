using MegaVM;
using MegaVM.Execution;

namespace MegaLisp;

public struct Symbol
{
    public readonly int SymbolId;
    public Symbol(int id) => SymbolId = id;
    
    public static bool operator== (Symbol a, Symbol b) =>  a.SymbolId == b.SymbolId;
    public static bool operator!= (Symbol a, Symbol b) =>  a.SymbolId != b.SymbolId;
}

public class Engine
{
    MegaVM.Object image = new ();
    private Executer ex;
    public Engine()
    {
        ex = new(image, new());
    }
    public void Eval(string code)
    {
        var parser = new Parser();
        ReadOnlySpan<char> code2 = code;
        code2 = parser.TokenizeExpression(code2, out var tokenGroup);
        if (tokenGroup.Car is Token subToken)
        {
            uint offset = (uint)image.Instructions.Count;
            Eval(subToken);
            image.Op("halt");
            ex.ExecuteAt(offset);
        }
    }

    int symbolCounter = 10000;
    readonly Dictionary<string, Symbol> symbols = new ();
    public Symbol add => resolveSymbol("+");
    public Symbol sub => resolveSymbol("-");
    public Symbol mul => resolveSymbol("*");
    public Symbol div => resolveSymbol("/");
    public Symbol cons => resolveSymbol("cons");
    

    
    Symbol resolveSymbol(string name)
    {
        if (symbols.TryGetValue(name, out var sym)) return sym;
        return symbols[name] = new Symbol(symbolCounter += 1);
    }

    public void EvalArg(Token tokenGroup)
    {
        if (tokenGroup.Car != null)
        {
            Eval(tokenGroup.Car);
            return;
        }
        if (int.TryParse(tokenGroup.Data, out var val))
        {
            image.Op("ldi", (ulong)val);
            return;
        }

        throw new Exception("!!");
    }

    public void Eval(Token tokenGroup)
    {
        if (tokenGroup.Data == null)
            throw new Exception("");//Dont know how to handle
        var sym = resolveSymbol(tokenGroup.Data);
        if (sym == add || sym == mul || sym == cons)
        {
            var subToken = tokenGroup.Cons;
            while (subToken != null)
            {
                EvalArg(subToken);
                subToken = subToken.Cons;
            }
            if (sym == add)
                image.Op("addi");
            else if( sym == mul)
                image.Op("muli");
            else if (sym == cons)
                image.Op("cons");
            else throw new Exception("Unknown symbol");
        }
    }
}