using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaVM.Execution
{
    public enum ValueKind
    {
        Void,
        UInt,
        Real,
        Struct,
        Pointer,
    }

    public class Value
    {
        public ValueKind ValueKind;

        public UInt64 UIntValue;
        public double RealValue;

        public Value[] Fields = new Value[0];

        public Value Content;

        public override string ToString()
        {
            switch (ValueKind)
            {
                case ValueKind.UInt:
                    return UIntValue.ToString();
                case ValueKind.Real:
                    return RealValue.ToString();
                default:
                    return "<special>";
            }
        }

        internal static Value Int(UInt64 argument)
        {
            return new Value { ValueKind = ValueKind.UInt, UIntValue = argument };
        }

        internal static Value Real(UInt64 argument)
        {
            var result = BitConverter.Int64BitsToDouble((Int64)argument);
            return new Value { ValueKind = ValueKind.Real, RealValue = result };
        }

        private static Value Struct(Value[] fields)
        {
            return new Value { ValueKind = ValueKind.Struct, Fields = fields };
        }

        private static Value Array(uint length, TypeDef typeDef)
        {
            throw new NotImplementedException();
        }

        private static Value Pointer(int v, Value value)
        {
            return new Value { ValueKind = ValueKind.Pointer, Content = value };
        }

        internal UInt64 AsUInt()
        {
            return UIntValue;
        }

        internal Int64 AsInt()
        {
            return (Int64)UIntValue;
        }

        internal Value Cast(ValueKind kind)
        {
            return this;
        }

        internal static Value DefaultValue(Object obj, TypeDef typeDef)
        {
            switch (typeDef.Kind)
            {
                case TypeKind.Pointer:
                case TypeKind.Void:
                    return Int(0).Cast(ValueKind.Void);
                case TypeKind.Int:
                    return Int(0);
                case TypeKind.Real:
                    return Real(0);
                case TypeKind.Struct:
                    return Struct(typeDef.Fields.Select(f => Value.DefaultValue(obj, obj.Types[(int)f])).ToArray());
                case TypeKind.Array:
                    return Array(typeDef.Length, obj.Types[(int)typeDef.BaseType]);
                default:
                    throw new Exception("Failed default");
            }
        }

        internal static Value AllocatePointer(Object obj, TypeDef typeDef)
        {
            if (typeDef.Kind == TypeKind.Pointer)
                return Pointer(obj.Types.IndexOf(typeDef), DefaultValue(obj, obj.Types[(int)typeDef.BaseType]));
            else
                throw new Exception("Not a pointer type");
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

                    localsStack.Push(sym.Locals.Select(x => Value.DefaultValue(obj, obj.Types[(int)x])).ToArray());

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

                case "new": return inst => stack.Push(Value.AllocatePointer(obj, obj.Types[(int)inst.Argument]));
                case "stfld": return inst =>
                {
                    var ptr = stack.Pop();
                    var value = stack.Pop();
                    ptr.Content.Fields[(int)inst.Argument] = value;
                };
                case "ldfld": return inst =>
                {
                    var ptr = stack.Pop();
                    var value = ptr.Content.Fields[(int)inst.Argument];
                    stack.Push(value);
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

                case "jmp": return inst => nextInstr = (UInt32)inst.Argument;
                case "beqi": return inst => DoICmp((a, b) => a == b, inst.Argument);
                case "blti": return inst => DoICmp((a, b) => a < b, inst.Argument);
                case "bltui": return inst => DoICmp((a, b) => (Int64)a < (Int64)b, inst.Argument);
            }

            throw new NotImplementedException(sym.Name);
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

        private void ExecuteAt(UInt32 offset)
        {
            int current = (int)offset;

            while (true)
            {
                nextInstr = (UInt32)current + 1;

                var instr = obj.Instructions[current];
                CallActions[instr.Symbol](instr);

                if (nextInstr == 0xFFFFFFFF)
                    break;

                current = (int)nextInstr;
            }
        }

        public void Execute()
        {
            ExecuteAt(0);
        }
    }
}