using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaVM.Execution
{
    public enum ValueKind
    {
        UInt,
        Real,
        Special,
    }

    public class Value
    {
        public Value(object obj) => ValueData = obj;
        public Value(){}
        public object ValueData { get; set; }
        
        public override string ToString()
        {
            return ValueData.ToString();
        }

        internal static Value Int(UInt64 argument) =>  new Value { ValueData = argument};
        
        internal static Value Real(UInt64 argument) => new Value { ValueData = argument };
        
        internal UInt64 AsUInt() => (UInt64)ValueData;
        

        internal Int64 AsInt() => (Int64)ValueData;
        
        internal static Value DefaultValue(TypeDef typeDef)
        {
            return Int(0); // TODO
        }
    }

    public class Executer
    {
        Stack<Value> stack = new Stack<Value>();
        Dictionary<UInt32, Action<Instruction>> CallActions = new Dictionary<uint, Action<Instruction>>();
        Object obj;

        Stack<Value[]> argumentStack = new Stack<Value[]>();
        Stack<Value[]> localsStack = new Stack<Value[]>();
        private Stack<UInt32> returnStack = new Stack<UInt32>();
        private UInt32 nextInstr;

        public Executer(Object obj, Dictionary<string, Action<Instruction, Stack<Value>>> imports)
        {
            this.obj = obj;

            for (int i = 0; i < obj.Symbols.Count; i++)
            {
                var sym = obj.Symbols[i];

                Action<Instruction> act = (inst) =>
                {
                    var args = new List<Value>();

                    for (int i = 0; i < sym.Parameters.Length; i++)
                        args.Add(stack.Pop());
                    argumentStack.Push(args.ToArray());

                    localsStack.Push(sym.Locals.Select(x => Value.DefaultValue(obj.Types[(int)x])).ToArray());
                    
                    returnStack.Push(nextInstr);

                    nextInstr = sym.Offset;
                };

                switch (sym.Type)
                {
                    case SymbolType.Builtin:
                        act = ResolveBuiltin(sym);
                        break;
                    case SymbolType.Import:
                        act = ResolveImport(sym, imports);
                        break;
                }

                CallActions[(UInt32)i] = act;
            }
        }

        private Action<Instruction> ResolveBuiltin(Symbol sym)
        {
            switch (sym.Name)
            {
                case "ldi": return inst => stack.Push(Value.Int(inst.Argument));
                case "ldr": return inst => stack.Push(Value.Real(inst.Argument));
                case "pop": return inst => stack.Pop();
                case "ret": return inst => { argumentStack.Pop(); nextInstr = returnStack.Pop(); };
                case "halt": return inst => nextInstr = 0xFFFFFFFF;

                case "ldarg": return inst => stack.Push(argumentStack.Peek()[(int)inst.Argument]);

                case "ldloc": return inst => stack.Push(localsStack.Peek()[(int)inst.Argument]);
                case "stloc": return inst => localsStack.Peek()[(int)inst.Argument] = stack.Pop();

                case "addi": return inst => DoIBin((a, b) => a + b);
                case "subi": return inst => DoIBin((a, b) => a - b);
                case "muli": return inst => DoIBin((a, b) => a * b);
                case "divi": return inst => DoIBin((a, b) => a / b);
                
                case "andi": return inst => DoIBin((a, b) => a & b);
                case "xori": return inst => DoIBin((a, b) => a ^ b);
                case "ori": return inst => DoIBin((a, b) => a | b);

                case "lsli": return inst => DoIBin((a, b) => a << (int)b);
                case "lsri": return inst => DoIBin((a, b) => a >> (int)b);
                case "asri": return inst => DoIBin((a, b) => (UInt64)((Int64)a >> (int)b));

                case "jmp": return inst => nextInstr = (UInt32)inst.Argument;
                case "beqi": return inst => DoICmp((a, b) => a == b, inst.Argument);
                case "blti": return inst => DoICmp((a, b) => a < b, inst.Argument);
                case "bltui": return inst => DoICmp((a, b) => (Int64)a < (Int64)b, inst.Argument);
            }

            throw new NotImplementedException(sym.Name);
        }

        private void DoObj(Func<object, object, object> func)
        {
            var b = stack.Pop();
            var a = stack.Pop();
            stack.Push( new Value(func(a.ValueData, b.ValueData)));
        }
        
        private void DoIBin(Func<UInt64, UInt64, UInt64> func)
        {
            var b = stack.Pop();
            var a = stack.Pop();
            stack.Push(Value.Int(func(a.AsUInt(), b.AsUInt())));
        }

        private void DoICmp(Func<UInt64, UInt64, bool> func, UInt64 nextAddr)
        {
            var b = stack.Pop();
            var a = stack.Pop();
            if (func(a.AsUInt(), b.AsUInt()))
                nextInstr = (UInt32)nextAddr;
        }

        private Action<Instruction> ResolveImport(Symbol sym, Dictionary<string, Action<Instruction, Stack<Value>>> imports)
        {
            if (imports.ContainsKey(sym.Name))
                return (inst) => imports[sym.Name](inst, stack);

            throw new NotImplementedException();
        }

        public int ExecuteAt(UInt32 offset)
        {
            int current = (int)offset;

            while (true)
            {
                nextInstr = (UInt32)current + 1;

                var instr = obj.Instructions[current];
                var sym = obj.Symbols[(int)instr.Symbol];
                if (sym.Type == SymbolType.Builtin)
                {
                    ResolveBuiltin(sym)(instr);
                }
                else if (CallActions.TryGetValue(instr.Symbol, out var a))
                {
                    a(instr);
                }
                //CallActions[instr.Symbol](instr);

                if (nextInstr == 0xFFFFFFFF)
                    break;

                current = (int)nextInstr;
            }

            return current;
        }

        public int Execute()
        {
            return ExecuteAt(0);
        }
    }
}