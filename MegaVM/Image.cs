using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MegaVM
{
    public class Header
    {
        public UInt32 Magic;

        public UInt32 EntryPoint;

        public UInt32 TypeCount;
        public UInt32 SymbolCount;
        public UInt32 TextSize;
        public UInt32 DataSize;
    }

    public enum TypeKind
    {
        // Primitives
        Void,
        Int,
        Real,

        Array,
        OpenArray,
        Struct,
        Pointer,
        Value,
        Function
    }

    public class TypeDef
    {
        public string Name;
        public TypeKind Kind;

        public UInt32 Length; // Primitive=bytes, Array=elementcount
        public UInt32 ElementType; // Pointer,Array,OpenArray
        
        public UInt32[] Fields = Array.Empty<UInt32>(); // Function, Struct
        public UInt32[] ReturnType = Array.Empty<UInt32>(); // Function
    }

    public enum SymbolType
    {
        Builtin,
        Import,
        Export,
        Local,
        Global
    }

    public class Symbol
    {
        public string Name;
        public SymbolType Type;
        public UInt32 ReturnType;
        public UInt32[] Parameters = Array.Empty<UInt32>();
        public UInt32[] Locals = Array.Empty<UInt32>();
        public Execution.Value Value;

        public override string ToString() => $"sym: {Name}: {Type}";
    }

    public class Instruction
    {
        public Instruction(Image image) => this.image = image;
        public UInt32 Symbol;
        public UInt64 Argument;
        internal Image image;
        public override string ToString() => $"{image.Symbols[(int)Symbol].Name} {Argument}";
    }

    public class Image
    {
        public List<TypeDef> Types { get; } = new List<TypeDef>();
        public readonly uint VoidType;
        public readonly uint IntType;
        public readonly uint RealType;
        public readonly uint ValueType;
        
        public readonly uint InstructionType;
        public readonly uint InstructionArrayType;

        public readonly uint IntBinaryOp;
        public List<Symbol> Symbols { get; } = new List<Symbol>();

        //public List<Instruction> Instructions { get; } = new List<Instruction>();
        public byte[] Data { get; set; } = new byte[0];

        public Image()
        {
            VoidType = DefinePrimitive(TypeKind.Void, "void");
            IntType = DefinePrimitive(TypeKind.Int, "int");
            RealType = DefinePrimitive(TypeKind.Real, "Real");
            ValueType = DefinePrimitive(TypeKind.Value, "Value");
            
            InstructionType =  DefineStruct( "Instruction", IntType, IntType);
            InstructionArrayType = DefineArrayType(InstructionType);
            IntBinaryOp = DefineFunctionType(IntType, IntType, IntType);
        }

        public uint DefineType(TypeDef def)
        {
            var typeid = Types.Count;
            Types.Add(def);
            return (uint)typeid;
        }
        public uint DefinePrimitive(TypeKind kind, string name) =>  DefineType(new TypeDef{Kind = kind, Name = name});
        public uint DefineStruct(string name, params uint[] fields) =>  DefineType(new TypeDef{Name = name, Kind = TypeKind.Struct, Fields = fields});
        public uint DefineFunctionType(uint returnType, params uint[] argumentTypes)
        {
            var t = new TypeDef {Kind = TypeKind.Function, Fields = argumentTypes, ReturnType = new[] {returnType}};
            return DefineType(t);
        }

        public uint DefineArrayType(uint elementType) =>
            DefineType(new TypeDef {Kind = TypeKind.OpenArray, ElementType = elementType});
        public uint DefineArrayType(uint elementType, uint length) =>
            DefineType(new TypeDef {Kind = TypeKind.Array, Length = length, ElementType = elementType});

        private static void Serialize(BinaryWriter writer, String sym)
        {
            Span<byte> result = stackalloc byte[4];
            var bytes = Encoding.UTF8.GetBytes(sym);

            BinaryPrimitives.WriteUInt32LittleEndian(result, (UInt32)bytes.Length);

            writer.Write(result);
            writer.Write(bytes);
        }

        private static void Serialize(BinaryWriter writer, UInt32[] values)
        {
            Span<byte> result = stackalloc byte[4 + 4 * values.Length];

            BinaryPrimitives.WriteUInt32LittleEndian(result, (UInt32)values.Length);
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(4 + i * 4), values[i]);

            writer.Write(result);
        }

        private static void Serialize(BinaryWriter writer, TypeDef typedef)
        {
            Span<byte> result = stackalloc byte[12];

            BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(0), (UInt32)typedef.Kind);
            BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(4), typedef.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(8), typedef.ElementType);

            writer.Write(result);
            Serialize(writer, typedef.Name);
            Serialize(writer, typedef.Fields);
        }

        private static void Serialize(BinaryWriter writer, Symbol sym)
        {
            Span<byte> result = stackalloc byte[12];

            BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(0), (UInt32)sym.Type);
            BinaryPrimitives.WriteUInt32LittleEndian(result.Slice(8), sym.ReturnType);

            writer.Write(result);
            Serialize(writer, sym.Name);
            Serialize(writer, sym.Parameters);
            Serialize(writer, sym.Locals);
        }

        private static void Serialize(BinaryWriter writer, Instruction instr)
        {
            Span<byte> result = stackalloc byte[12];

            BinaryPrimitives.WriteUInt32LittleEndian(result, instr.Symbol);
            BinaryPrimitives.WriteUInt64LittleEndian(result.Slice(4), instr.Argument);

            writer.Write(result);
        }

        public void Serialize(BinaryWriter writer)
        {
            // Parse header
            Span<byte> header = stackalloc byte[4 * 6];

            UInt32 magic = (UInt32)0x12345678;
            UInt32 typeCount = (UInt32)Types.Count;
            UInt32 symbolCount = (UInt32)Symbols.Count;
            //UInt32 instrCount = (UInt32)Instructions.Count;
            UInt32 dataMemoryBytes = (UInt32)Data.Length;
            UInt32 dataBytes = (UInt32)CountNonZero(Data);

            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 0), magic);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 1), typeCount);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 2), symbolCount);
            //BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 3), instrCount);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 4), dataMemoryBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4 * 5), dataBytes);

            writer.Write(header);

            // Types
            Types.ForEach(obj => Serialize(writer, obj));

            // Symbols
            Symbols.ForEach(obj => Serialize(writer, obj));

            // Instructions
            //Instructions.ForEach(obj => Serialize(writer, obj));

            // Data
            writer.Write(Data.AsSpan().Slice(0, (int)dataBytes));
        }

        private static int CountNonZero(byte[] data)
        {
            var count = data.Length;

            for (int i = data.Length - 1; i >= 0; i--)
                if (data[i] != 0)
                    return i + 1;

            return 0;
        }

        private static string DecodeString(ref ReadOnlySpan<byte> data)
        {
            var nameLen = BinaryPrimitives.ReadUInt32LittleEndian(data);
            var result = Encoding.UTF8.GetString(data.Slice(4, (int)nameLen));
            data = data.Slice(4 + (int)nameLen);
            return result;
        }

        private static UInt32[] DecodeArray(ref ReadOnlySpan<byte> data)
        {
            var paramLen = BinaryPrimitives.ReadUInt32LittleEndian(data);
            var param = new UInt32[paramLen];

            for (var i2 = 0; i2 < paramLen; i2++)
                param[i2] = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * i2 + 4));
            data = data.Slice(4 + 4 * (int)paramLen);

            return param;
        }

        private static Symbol DecodeSym(ref ReadOnlySpan<byte> data)
        {
            var sym = new Symbol
            {
                Type = (SymbolType)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 0)),
                //Offset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 1)),
                ReturnType = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 2))
            };
            data = data.Slice(4 * 3);

            sym.Name = DecodeString(ref data);
            sym.Parameters = DecodeArray(ref data);
            sym.Locals = DecodeArray(ref data);

            return sym;
        }

        private static TypeDef DecodeTypeDef(ref ReadOnlySpan<byte> data)
        {
            var def = new TypeDef
            {
                Kind = (TypeKind)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 0)),
                Length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 1)),
                ElementType = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 2))
            };
            data = data.Slice(4 * 3);

            def.Name = DecodeString(ref data);
            def.Fields = DecodeArray(ref data);

            return def;
        }

        public static Image Deserialize(ReadOnlySpan<byte> data)
        {
            // Parse header
            var magic = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 0));
            var typeCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 1));
            var symbolCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 2));
            var instrCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 3));
            var dataMemoryBytes = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 4));
            var dataBytes = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4 * 5));

            var result = new Image();

            data = data.Slice(4 * 6);

            // Types
            for (int i = 0; i < typeCount; i++)
                result.Types.Add(DecodeTypeDef(ref data));

            // Symbols
            for (int i = 0; i < symbolCount; i++)
                result.Symbols.Add(DecodeSym(ref data));

            // Instructions
            for (int i = 0; i < instrCount; i++)
            {
                //result.Instructions.Add(new Instruction(result)
                //{
                //    Symbol = BinaryPrimitives.ReadUInt32LittleEndian(data),
                //    Argument = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(4))
                //});
                //data = data.Slice(12);
            }

            // Data
            result.Data = new byte[dataMemoryBytes];
            data.CopyTo(result.Data.AsSpan().Slice(0, (int)dataBytes));

            return result;
        }

        

        public Symbol GetSymbol(string name)
        {
            var result = Symbols.FirstOrDefault(sym => sym.Name == name);
            if (result == null)
            {
                result = new Symbol { Name = name, Type = SymbolType.Import };
                Symbols.Add(result);
            }
            return result;
        }
    }
}