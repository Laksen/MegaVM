using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaVM.Execution
{
    
    public class Value
    {
        public uint ValueKind;

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
        
        private static Value Struct(uint type, Value[] fields)
        {
            return new Value { ValueKind = type, ValueData = fields };
        }

        public static Value Array(Image obj, uint length, uint typeDef)
        {
            var elemType = obj.Types[(int) typeDef].ElementType;
            var array = Enumerable.Range(0, (int)length)
                .Select(i => Value.DefaultValue(obj,  elemType))
                .ToArray();
            return new Value {ValueKind = typeDef, ValueData = array};
        }
        public static Value DefaultValue(Image obj, uint typeId)
        {
            var type = obj.Types[(int)typeId];
            switch (type.Kind)
            {
                case TypeKind.Value:
                case TypeKind.Pointer:    
                case TypeKind.Void:
                    return Int(0);
                case TypeKind.Int:
                    return Int(0);
                case TypeKind.Real:
                    return Real(0);
                case TypeKind.Struct:
                    return Struct(typeId, type.Fields.Select(f => Value.DefaultValue(obj, f)).ToArray());
                case TypeKind.Array:
                    return Array(obj, type.Length, typeId);
                case TypeKind.OpenArray:
                    return Array(obj, 0, typeId);
                
                default:
                    throw new Exception("Failed default");
            }
        }
    }

    public class Executer
    {
        public Stack<Value> Stack => stack;
        Stack<Value> stack = new Stack<Value>();
        Dictionary<UInt32, Action<Instruction>> CallActions = new Dictionary<uint, Action<Instruction>>();
        Image obj;

        Stack<Value[]> argumentStack = new Stack<Value[]>();
        Stack<Value[]> localsStack = new Stack<Value[]>();
        private Stack<(uint symbol, uint offset)> returnStack = new Stack<(uint symbol, uint offset)>();
        private (uint symbol, uint offset) nextInstr;

        public Executer(Image obj, Dictionary<string, Action<Instruction, Stack<Value>>> imports)
        {
            this.obj = obj;

            for (int i = 0; i < obj.Symbols.Count; i++)
            {
                int _i = i;
                var sym = obj.Symbols[i];

                Action<Instruction> act = (inst) =>
                {
                    var args = new List<Value>();

                    for (int i = 0; i < sym.Parameters.Length; i++)
                        args.Add(stack.Pop());
                    argumentStack.Push(args.ToArray());

                    localsStack.Push(sym.Locals.Select(x => Value.DefaultValue(obj,x)).ToArray());

                    returnStack.Push(nextInstr);

                    nextInstr = ((uint)_i, 0);//sym.Offset;
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
                case "dup": return inst => stack.Push(stack.Peek());
                case "swap": return inst =>
                {
                    var a = stack.Pop();
                    var b = stack.Pop();
                    stack.Push(a);
                    stack.Push(b);
                };
                case "ldi": return inst => stack.Push(Value.Int(inst.Argument));
                case "ldr": return inst => stack.Push(Value.Real(inst.Argument));
                case "pop": return inst => stack.Pop();
                case "ret": return inst => { argumentStack.Pop(); nextInstr = returnStack.Pop(); };
                case "halt": return inst => nextInstr = (0xFFFFFFFF, 0);

                case "ldarg": return inst => stack.Push(argumentStack.Peek()[(int)inst.Argument]);

                case "ldloc": return inst => stack.Push(localsStack.Peek()[(int)inst.Argument]);
                case "stloc": return inst => localsStack.Peek()[(int)inst.Argument] = stack.Pop();

                case "new": return inst => stack.Push(Value.DefaultValue(obj, (uint)inst.Argument));
                case "stfld": return inst =>
                {
                    var value = stack.Pop();
                    var ptr = stack.Pop();
                    ((Array)ptr.ValueData).SetValue(value, (int)inst.Argument);
                };
                case "ldfld": return inst =>
                {
                    var ptr = stack.Pop();
                    var value = ((Array)ptr.ValueData).GetValue((int)inst.Argument);
                    stack.Push(new Value(value));
                };

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

                case "jmp": return inst => nextInstr = (nextInstr.symbol, (UInt32)inst.Argument);
                case "beqi": return inst => DoICmpJmp((a, b) => a == b, inst.Argument);
                case "blti": return inst => DoICmpJmp((a, b) => a < b, inst.Argument);
                case "bltui": return inst => DoICmpJmp((a, b) => (Int64)a < (Int64)b, inst.Argument);
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

        private void DoICmpJmp(Func<UInt64, UInt64, bool> func, UInt64 nextAddr)
        {
            var b = stack.Pop();
            var a = stack.Pop();
            if (func(a.AsUInt(), b.AsUInt()))
                nextInstr = (nextInstr.symbol, (UInt32)nextAddr);
        }

        private Action<Instruction> ResolveImport(Symbol sym, Dictionary<string, Action<Instruction, Stack<Value>>> imports)
        {
            if (imports.ContainsKey(sym.Name))
                return (inst) => imports[sym.Name](inst, stack);

            throw new NotImplementedException();
        }

        public (uint, uint) ExecuteAt(uint symbolId)
        {
            (uint symbol, uint offset) current= (symbolId, 0);
            nextInstr = current;
            while (true)
            {
                var sym = obj.Symbols[(int)current.symbol];
                Assert.IsTrue(sym.Value.ValueKind == obj.InstructionArrayType);
                var instructions = (Value[])sym.Value.ValueData;
                
                nextInstr.offset = nextInstr.offset + 1;
                var instr = (Value[])instructions[current.offset].ValueData;
                
                var instrSym = instr[0].AsUInt();
                var argument = instr[1].AsUInt();
                var sym2 = obj.Symbols[(int)instrSym];
                if (sym2.Type == SymbolType.Builtin)
                {
                    ResolveBuiltin(sym2)(new Instruction(obj){Argument = argument, Symbol = (uint)instrSym});
                }
                else if (CallActions.TryGetValue((uint) instrSym, out var a))
                {
                    a(new Instruction(obj){Argument = argument, Symbol = (uint)instrSym});
                }
                //CallActions[instr.Symbol](instr);

                if (nextInstr.symbol == 0xFFFFFFFF)
                    break;

                current = nextInstr;
            }

            return current;
        }

        public void Execute()
        {
            ExecuteAt(0);
        }
    }

    public static class Assert
    {
        public static void IsTrue(bool truth)
        {
            if (!truth)
                throw new Exception();
        } 
    }
}