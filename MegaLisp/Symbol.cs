namespace MegaLisp;

public struct Symbol
{
    public readonly int SymbolId;
    public bool IsFunction = false;
    public bool IsMacro = false;
    public Symbol(int id) => SymbolId = id;
    
    public static bool operator== (Symbol a, Symbol b) =>  a.SymbolId == b.SymbolId;
    public static bool operator!= (Symbol a, Symbol b) =>  a.SymbolId != b.SymbolId;
}