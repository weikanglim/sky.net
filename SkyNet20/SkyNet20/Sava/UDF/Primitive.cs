using System;
using System.Collections.Generic;
using System.Text;
using ProtoBuf;

/// <summary>
/// Taken from https://stackoverflow.com/questions/10152539/protobuf-net-serialization-deserialization/10153871#10153871 to
/// handle generic type values that supports ProtoBuf serialization.
/// </summary>
namespace SkyNet20.Sava.UDF
{
    [ProtoContract]
    [ProtoInclude(3, typeof(Primitive<int>))]
    [ProtoInclude(4, typeof(Primitive<double>))]
    [ProtoInclude(5, typeof(Primitive<string>))]
    public abstract class Primitive
    {
        public abstract object UntypedValue { get; set; }
        public static Primitive<T> Create<T>(T value)
        {
            return new Primitive<T> { Value = value };
        }
        public static Primitive CreateDynamic(object value)
        {
            Type type = value.GetType();
            switch (Type.GetTypeCode(value.GetType()))
            {
                // special cases
                case TypeCode.Int32: return Create((int)value);
                case TypeCode.Double: return Create((double)value);
                case TypeCode.String: return Create((string)value);
                // fallback in case we forget to add one, or it isn't a TypeCode
                default:
                    Primitive param = (Primitive)Activator.CreateInstance(
                        typeof(Primitive<>).MakeGenericType(type));
                    param.UntypedValue = value;
                    return param;
            }
        }
    }
    [ProtoContract]
    public sealed class Primitive<T> : Primitive
    {
        [ProtoMember(1)]
        public T Value { get; set; }
        public override object UntypedValue
        {
            get { return Value; }
            set { Value = (T)value; }
        }
    }

}
