using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MegaVM.Execution;

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

                    obj = Image.Deserialize(mem.ToArray().AsSpan());
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

        private static Image MakeFib()
        {
            var img = new Image();

            var obj = img.Define("main", Array.Empty<uint>(), img.VoidType);

            obj.Push(32);
            obj.Call("fib");
            obj.Call("printI");
            obj.Op("halt");
            obj.Write();

            obj = img.Define("fib", new[] { img.IntType }, img.IntType);
            
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
            obj.Write();

            return img;
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

    public class FunctionBuilder
    {
        public List<Instruction> Instructions = new List<Instruction>();
        public FunctionBuilder(Image image, Symbol functionSymbol)
        {
            this.image = image;
            this.functionSymbol = functionSymbol;
        }

        public void Write()
        {
            var value = Value.Array(image, (uint)Instructions.Count, image.InstructionArrayType);
            var data = (Value[]) value.ValueData;
            for (int i = 0; i < Instructions.Count; i++)
            {
                var instr = (Value[]) data[i].ValueData;
                instr[0] = Value.Int(Instructions[i].Symbol);
                instr[1] = Value.Int(Instructions[i].Argument);
            }
            functionSymbol.Value = value;
        }
        private readonly Image image;
        private readonly Symbol functionSymbol;

        public UInt32 TypeDef(string name, TypeKind kind)
        {
            var result = (UInt32)image.Types.Count;

            image.Types.Add(new MegaVM.TypeDef { Name = name, Kind = kind });

            return result;
        }

        public void Push(int value)
        {
            Op("ldi", (UInt64)value);
        }

        public void Call(string name)
        {
            var opSym = image.GetSymbol(name);
            var idx = image.Symbols.IndexOf(opSym);

            var result = new Instruction(image) { Symbol = (UInt32)idx };
            Instructions.Add(result);
        }

        public Instruction Op(string name, double value) =>
            Op(name, BitConverter.DoubleToInt64Bits(value));

        public  Instruction Op( string name, UInt64 value = 0)
        {
            var opSym = image.GetSymbol(name);
            opSym.Type = SymbolType.Builtin;

            var idx = image.Symbols.IndexOf(opSym);

            var result = new Instruction(image) { Symbol = (UInt32)idx, Argument = value };
            Instructions.Add(result);
            return result;
        }
        
        public Instruction Op(Symbol opSym)
        {
            var idx = image.Symbols.IndexOf(opSym);
            var result = new Instruction(image) { Symbol = (UInt32)idx, Argument = 0 };
            Instructions.Add(result);
            return result;
        }
    }
    
    public static class ObjectHelpers
    {
        

        public static FunctionBuilder Define(this Image obj, string name, UInt32[] argTypes, UInt32 resultType)
        {
            var sym = obj.GetSymbol(name);
            sym.Type = SymbolType.Local;
            sym.Parameters = argTypes;
            sym.ReturnType = resultType;
            
            return new FunctionBuilder(obj, sym);
        }
    }
}
