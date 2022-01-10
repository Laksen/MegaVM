using MegaVM;
using MegaVM.Execution;

namespace MegaLisp;

public class Engine
{
    readonly Image image = new ();
    readonly Executer ex;
    private uint ConsType;
    
    public Engine()
    {
        var symbols = new Dictionary<string, Action<Instruction, Stack<Value>>>();
        ConsType = image.DefineStruct("cons", image.ValueType, image.ValueType);
        
        // cons: (a b)
        var bld = image.Define("cons", new []{image.ValueType, image.ValueType}, ConsType);
        bld.Op("new", ConsType);
        bld.Op("dup", ConsType);
        bld.Op("dup", ConsType);
        bld.Op("ldarg", 1);
        bld.Op("stfld", 0);
        bld.Op("ldarg", 0);
        bld.Op("stfld", 1);
        bld.Op("ret");
        bld.Write();
        
        bld = image.Define("car", new []{ConsType}, image.ValueType);
        bld.Op("ldarg", 0);
        bld.Op("ldfld", 0);
        bld.Op("ret");
        bld.Write();

        bld = image.Define("cdr", new []{ConsType}, image.ValueType);
        bld.Op("ldarg", 0);
        bld.Op("ldfld", 1);
        bld.Op("ret");
        bld.Write();

        bld = image.Define("+", new []{image.ValueType,image.ValueType}, image.ValueType);
        bld.Op("ldarg", 0);
        bld.Op("ldarg", 1);
        bld.Op("addi", 0);
        bld.Op("ret");
        bld.Write();

        bld = image.Define("*", new []{image.ValueType, image.ValueType}, image.ValueType);
        bld.Op("ldarg", 0);
        bld.Op("ldarg", 1);
        bld.Op("muli", 0);
        bld.Op("ret");
        bld.Write();

        bld = image.Define("quote", new []{image.ValueType, image.ValueType}, image.ValueType);
        bld.Op("ldarg", 0);
        bld.Op("ret");
        bld.Write();

        var sym2 = resolveSymbol("quote");
        sym2.IsMacro = true;
        this.symbols["quote"] = sym2;
        
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
        var bld = image.Define("eval", Array.Empty<uint>(), image.VoidType);
        if (tokenGroup.Car is Token subToken)
        {
            Eval(bld, subToken);
            bld.Op("halt");
            bld.Write();
            uint offset = (uint) image.Symbols.IndexOf(image.GetSymbol("eval"));
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

    public void EvalArg(FunctionBuilder bld, Token tokenGroup)
    {
        if (tokenGroup.Car != null)
        {
            Eval(bld, tokenGroup.Car);
            return;
        }
        if (int.TryParse(tokenGroup.Data, out var val))
        {
            bld.Op("ldi", (ulong)val);
            return;
        }
        if (double.TryParse(tokenGroup.Data, out var valr))
        {
            bld.Op("ldr", (double)valr);
            return;
        }

        throw new Exception("!!");
    }

    Value CreateCons() => Value.DefaultValue(image,  ConsType);
    

    Value EvalMacroArg(Token subToken)
    {
        var cons = CreateCons();
        var arr = (Array) cons.ValueData;

        if (subToken.Car != null)
        {
            arr.SetValue(EvalMacroArg(subToken.Car), 0);
        }
        else
        {
            if (int.TryParse(subToken.Data, out var val))
                arr.SetValue(new Value {ValueData = val}, 0);
            if (double.TryParse(subToken.Data, out var valr))
                arr.SetValue(new Value {ValueData = valr}, 0);
        }

        if (subToken.Cons != null)
            arr.SetValue(EvalMacroArg(subToken.Cons), 1);
        
        return cons;
    }

    private int symCounter = 0;
    Symbol GenSym()
    {
        var name = "gsym" + symCounter;
        throw new NotImplementedException();
    }

    public void Eval(FunctionBuilder bld, Token tokenGroup)
    {
        if (tokenGroup.Data == null)
            throw new Exception("");//Dont know how to handle
        var sym = resolveSymbol(tokenGroup.Data);
        if (sym.IsMacro)
        {
            var subToken = tokenGroup.Cons;
            while (subToken != null)
            {
                Value val = EvalMacroArg(subToken);
                Symbol s = GenSym();
                subToken = subToken.Cons;
            }
            
        }
        if (sym.IsFunction)
        {
            var subToken = tokenGroup.Cons;
            while (subToken != null)
            {
                EvalArg(bld, subToken);
                subToken = subToken.Cons;
            }
            bld.Call(tokenGroup.Data);
        }
    }
}