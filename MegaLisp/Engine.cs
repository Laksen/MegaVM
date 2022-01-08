using MegaVM;
using MegaVM.Execution;

namespace MegaLisp;

public struct Symbol
{
    public readonly int SymbolId;
    public bool IsFunction = false;
    public Symbol(int id) => SymbolId = id;
    
    public static bool operator== (Symbol a, Symbol b) =>  a.SymbolId == b.SymbolId;
    public static bool operator!= (Symbol a, Symbol b) =>  a.SymbolId != b.SymbolId;
}

public class Engine
{
    MegaVM.Image image = new ();
    private Executer ex;
    
    
    public int cons;
    
    public Engine()
    {
        var symbols = new Dictionary<string, Action<Instruction, Stack<Value>>>();
        image.Types.Add(new TypeDef
        {
            Fields = new []
            {
              image.ValueType,
              image.ValueType
            },
            Name = "cons",
            Kind = TypeKind.Struct
        });
        uint consType = (uint)image.Types.Count - 1;
        
        // cons: (a b)
        image.Define("cons", new []{image.ValueType, image.ValueType}, consType);
        image.Op("new", consType);
        image.Op("dup", consType);
        image.Op("dup", consType);
        image.Op("ldarg", 1);
        image.Op("stfld", 0);
        image.Op("ldarg", 0);
        image.Op("stfld", 1);
        image.Op("ret");
        
        image.Define("car", new []{consType}, image.ValueType);
        image.Op("ldarg", 0);
        image.Op("ldfld", 0);
        image.Op("ret");
        
        image.Define("cdr", new []{consType}, image.ValueType);
        image.Op("ldarg", 0);
        image.Op("ldfld", 1);
        image.Op("ret");
        
        image.Define("+", new []{image.ValueType,image.ValueType}, image.ValueType);
        image.Op("ldarg", 0);
        image.Op("ldarg", 1);
        image.Op("addi", 0);
        image.Op("ret");
        
        image.Define("*", new []{image.ValueType,image.ValueType}, image.ValueType);
        image.Op("ldarg", 0);
        image.Op("ldarg", 1);
        image.Op("muli", 0);
        image.Op("ret");
        
        ex = new(image, symbols);
    }
    
    private void DoObj( Stack<Value> stack, Func<object, object, object> func)
    {
        var b = stack.Pop();
        var a = stack.Pop();
        stack.Push( new Value(func(a.ValueData, b.ValueData)));
    }
    
    public object Eval(string code)
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

        return ex.Stack.Pop();
    }

    int symbolCounter = 10000;
    readonly Dictionary<string, Symbol> symbols = new ();
 
    Symbol resolveSymbol(string name)
    {
        if (symbols.TryGetValue(name, out var sym))
            return sym;
        var funcSymbol = image.Symbols.FirstOrDefault(sym => sym.Name == name);;
        if (funcSymbol != null)
        {
            return symbols[name] = new Symbol(symbolCounter++) {IsFunction = true};
        }

        return default;
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
        if (double.TryParse(tokenGroup.Data, out var valr))
        {
            image.Op("ldr", (double)valr);
            return;
        }

        throw new Exception("!!");
    }

    public void Eval(Token tokenGroup)
    {
        if (tokenGroup.Data == null)
            throw new Exception("");//Dont know how to handle
        var sym = resolveSymbol(tokenGroup.Data);
        if (sym.IsFunction)
        {
            var subToken = tokenGroup.Cons;
            while (subToken != null)
            {
                EvalArg(subToken);
                subToken = subToken.Cons;
            }
            image.Call(tokenGroup.Data);
        }
    }
}