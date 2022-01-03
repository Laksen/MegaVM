using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MegaVM
{
    class Program
    {
        static void Main(string[] args)
        {
            var now = Stopwatch.StartNew();
            Console.WriteLine(fib(32));
            now.Stop();
            Console.WriteLine(now.Elapsed.TotalSeconds);

            var obj = MakeFib();

            using (var mem = new MemoryStream())
            {
                using (var sw = new BinaryWriter(mem))
                {
                    obj.Serialize(sw);
                    sw.Flush();

                    // Console.WriteLine(mem.Length);

                    obj = Object.Deserialize(mem.ToArray().AsSpan());
                }
            }

            try
            {
                var ex = new MegaVM.Execution.Executer(obj, new Dictionary<string, Action<Instruction, Stack<Execution.Value>>>()
                {
                    ["printI"] = (inst, stack) => doPrint(stack)
                });

                now = Stopwatch.StartNew();
                ex.Execute();
                now.Stop();
                Console.WriteLine(now.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        static void doPrint(Stack<Execution.Value> stack)
        {
            var value = stack.Pop();
            Console.WriteLine(value.ToString());
        }

        private static Object MakeFib()
        {
            var obj = new Object();

            var tdInt = obj.TypeDef("int", TypeKind.Int);

            obj.Push(32);
            obj.Call("fib");
            obj.Call("printI");
            obj.Op("halt");

            obj.Define("fib", new[] { tdInt }, tdInt);
            obj.Op("ldarg", 0);
            obj.Op("ldi", 1);
            var lEq1 = obj.Op("beqi", 0);
            obj.Op("ldarg", 0);
            obj.Op("ldi", 0);
            var lEq0 = obj.Op("beqi", 0);


            obj.Op("ldarg", 0);
            obj.Op("ldi", 1);
            obj.Op("subi");
            obj.Call("fib");
            obj.Op("ldarg", 0);
            obj.Op("ldi", 2);
            obj.Op("subi");
            obj.Call("fib");
            obj.Op("addi");
            obj.Op("ret");

            // lEq1
            lEq1.Argument = (UInt32)obj.Instructions.Count;
            obj.Op("ldi", 1);
            obj.Op("ret");
            // lEq0
            lEq0.Argument = (UInt32)obj.Instructions.Count;
            obj.Op("ldi", 0);
            obj.Op("ret");

            return obj;
        }

        static int fib(int i)
        {
            if (i == 1)
                return 1;
            else if (i == 0)
                return 0;
            else
                return fib(i - 1) + fib(i - 2);
        }
    }

    public static class ObjectHelpers
    {
        public static UInt32 TypeDef(this Object obj, string name, TypeKind kind)
        {
            var result = (UInt32)obj.Types.Count;

            obj.Types.Add(new MegaVM.TypeDef { Name = name, Kind = kind });

            return result;
        }

        public static void Push(this Object obj, int value)
        {
            obj.Op("ldi", (UInt64)value);
        }

        public static void Call(this Object obj, string name)
        {
            var opSym = obj.GetSymbol(name);
            var idx = obj.Symbols.IndexOf(opSym);

            var result = new Instruction { Symbol = (UInt32)idx };
            obj.Instructions.Add(result);
        }

        public static Instruction Op(this Object obj, string name, UInt64 value = 0)
        {
            var opSym = obj.GetSymbol(name);
            opSym.Type = SymbolType.Builtin;

            var idx = obj.Symbols.IndexOf(opSym);

            var result = new Instruction { Symbol = (UInt32)idx, Argument = value };
            obj.Instructions.Add(result);
            return result;
        }

        public static void Define(this Object obj, string name, UInt32[] argTypes, UInt32 resultType)
        {
            var sym = obj.GetSymbol(name);
            sym.Type = SymbolType.Local;
            sym.Offset = (UInt32)obj.Instructions.Count;
            sym.Parameters = argTypes;
            sym.ReturnType = resultType;
        }
    }
}
