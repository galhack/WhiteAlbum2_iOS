// SPDX-License-Identifier: Apache-2.0
using System;

// A script value with statically compiled operators, suitable for iOS NativeAOT.
public readonly struct Wa2Value : IEquatable<Wa2Value>, IFormattable
{
    private readonly object _value;
    private Wa2Value(object value) { _value = value; }
    public bool IsInteger => _value is null or int;
    public bool IsFloat => _value is float;
    private bool IsString => _value is string;

    public static implicit operator Wa2Value(int value) => new(value);
    public static implicit operator Wa2Value(float value) => new(value);
    public static implicit operator Wa2Value(string value) => new(value ?? "");
    public static implicit operator int(Wa2Value value) => value._value switch
    {
        int number => number,
        float number => (int)number,
        null => 0,
        _ => throw new InvalidOperationException("Script value is not numeric.")
    };
    public static implicit operator float(Wa2Value value) => value._value switch
    {
        int number => number,
        float number => number,
        null => 0,
        _ => throw new InvalidOperationException("Script value is not numeric.")
    };
    public static implicit operator string(Wa2Value value) => value._value as string
        ?? throw new InvalidOperationException("Script value is not text.");

    public static Wa2Value operator +(Wa2Value a, Wa2Value b)
    {
        if (a.IsString || b.IsString) return a.ToString() + b.ToString();
        if (a.IsInteger && b.IsInteger) return (int)a + (int)b;
        return (float)a + (float)b;
    }
    public static Wa2Value operator -(Wa2Value a, Wa2Value b)
    {
        if (a.IsInteger && b.IsInteger) return (int)a - (int)b;
        return (float)a - (float)b;
    }
    public static Wa2Value operator *(Wa2Value a, Wa2Value b)
    {
        if (a.IsInteger && b.IsInteger) return (int)a * (int)b;
        return (float)a * (float)b;
    }
    public static Wa2Value operator /(Wa2Value a, Wa2Value b)
    {
        if (a.IsInteger && b.IsInteger) return (int)a / (int)b;
        return (float)a / (float)b;
    }
    public static Wa2Value operator %(Wa2Value a, Wa2Value b)
    {
        if (a.IsInteger && b.IsInteger) return (int)a % (int)b;
        return (float)a % (float)b;
    }
    private static int Integer(Wa2Value value) => value.IsInteger ? (int)value
        : throw new InvalidOperationException("Bitwise script operands must be integers.");
    public static Wa2Value operator &(Wa2Value a, Wa2Value b) => Integer(a) & Integer(b);
    public static Wa2Value operator |(Wa2Value a, Wa2Value b) => Integer(a) | Integer(b);
    public static Wa2Value operator -(Wa2Value value) => value.IsInteger ? (Wa2Value)(-(int)value) : -(float)value;
    public static bool operator ==(Wa2Value a, Wa2Value b) => a.Equals(b);
    public static bool operator !=(Wa2Value a, Wa2Value b) => !a.Equals(b);
    public static bool operator <(Wa2Value a, Wa2Value b) => a.IsInteger && b.IsInteger ? (int)a < (int)b : (float)a < (float)b;
    public static bool operator >(Wa2Value a, Wa2Value b) => a.IsInteger && b.IsInteger ? (int)a > (int)b : (float)a > (float)b;
    public static bool operator <=(Wa2Value a, Wa2Value b) => a.IsInteger && b.IsInteger ? (int)a <= (int)b : (float)a <= (float)b;
    public static bool operator >=(Wa2Value a, Wa2Value b) => a.IsInteger && b.IsInteger ? (int)a >= (int)b : (float)a >= (float)b;

    public bool Equals(Wa2Value other)
    {
        if (IsString || other.IsString) return Equals(_value, other._value);
        return IsInteger && other.IsInteger ? (int)this == (int)other : (float)this == (float)other;
    }
    public override bool Equals(object other) => other is Wa2Value value && Equals(value);
    public override int GetHashCode() => IsString ? _value.GetHashCode() : ((float)this).GetHashCode();
    public override string ToString() => ToString(null, null);
    public string ToString(string format, IFormatProvider provider) => _value is IFormattable formattable
        ? formattable.ToString(format, provider) : _value?.ToString() ?? 0.ToString(format, provider);
}
