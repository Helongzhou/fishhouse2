#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Aquarium.Validation
{
    [Serializable] public sealed class NumericRoundtripDifference
    { public string path; public double before, after, absoluteError, allowedError; }
    [Serializable] public sealed class SnapshotComparison
    {
        public string name, failure, maxErrorPath;
        public bool passed;
        public int strictlyComparedValues, doubleValues;
        public double maxAbsoluteError;
        public List<NumericRoundtripDifference> nonzeroDoubleDifferences = new List<NumericRoundtripDifference>();
    }

    // Test-only JSON reader. Keeps numeric lexemes until the declared C# field type is known;
    // neither integer identity nor the original double ULP is lost through a generic number conversion.
    internal static class TypedSnapshotJson
    {
        private sealed class Value
        { public char Kind; public string Text; public Dictionary<string,Value> Fields; public List<Value> Elements; }
        private sealed class Reader
        {
            private readonly string text; private int index;
            internal Reader(string text) { this.text=text; }
            private void Space() { while(index<text.Length && char.IsWhiteSpace(text[index]))index++; }
            private char Take() { if(index>=text.Length)throw new FormatException("Unexpected JSON end.");return text[index++]; }
            private void Expect(char c) { Space();if(Take()!=c)throw new FormatException("Expected JSON token "+c); }
            private string String()
            {
                Expect('"');var result=new StringBuilder();
                while(true)
                {
                    char c=Take();if(c=='"')return result.ToString();
                    if(c=='\\')
                    {
                        c=Take();switch(c)
                        {
                            case '"':case '\\':case '/':result.Append(c);break;
                            case 'b':result.Append('\b');break;case 'f':result.Append('\f');break;
                            case 'n':result.Append('\n');break;case 'r':result.Append('\r');break;case 't':result.Append('\t');break;
                            case 'u':if(index+4>text.Length)throw new FormatException("Incomplete JSON escape.");
                                result.Append((char)int.Parse(text.Substring(index,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture));index+=4;break;
                            default:throw new FormatException("Unknown JSON escape.");
                        }
                    }
                    else { if(c<32)throw new FormatException("Unescaped JSON control character.");result.Append(c); }
                }
            }
            private Value Read()
            {
                Space();if(index>=text.Length)throw new FormatException("Missing JSON value.");char c=text[index];
                if(c=='{')
                {
                    index++;var value=new Value{Kind='o',Fields=new Dictionary<string,Value>(StringComparer.Ordinal)};Space();
                    if(index<text.Length&&text[index]=='}'){index++;return value;}
                    while(true){string key=String();Expect(':');Value field=Read();if(value.Fields.ContainsKey(key))throw new FormatException("Duplicate JSON field.");value.Fields.Add(key,field);Space();char separator=Take();if(separator=='}')return value;if(separator!=',')throw new FormatException("Invalid JSON object separator.");}
                }
                if(c=='[')
                {
                    index++;var value=new Value{Kind='a',Elements=new List<Value>()};Space();if(index<text.Length&&text[index]==']'){index++;return value;}
                    while(true){value.Elements.Add(Read());Space();char separator=Take();if(separator==']')return value;if(separator!=',')throw new FormatException("Invalid JSON array separator.");}
                }
                if(c=='"')return new Value{Kind='s',Text=String()};
                int start=index;while(index<text.Length&&text[index]!=','&&text[index]!=']'&&text[index]!='}'&&!char.IsWhiteSpace(text[index]))index++;
                string token=text.Substring(start,index-start);
                if(token=="null")return new Value{Kind='0'};
                if(token=="true"||token=="false")return new Value{Kind='b',Text=token};
                if(token.Length==0)throw new FormatException("Missing JSON primitive.");return new Value{Kind='n',Text=token};
            }
            internal Value All() { Value result=Read();Space();if(index!=text.Length)throw new FormatException("Trailing JSON tokens.");return result; }
        }
        internal static SnapshotComparison Compare(string name,string before,string after,Type declaredType)
        {
            var result=new SnapshotComparison{name=name};
            try { Visit(new Reader(before).All(),new Reader(after).All(),declaredType,"$",result);result.passed=true; }
            catch(Exception error){result.failure=error.Message;result.passed=false;}
            return result;
        }
        private static void Require(bool valid,string path,string message)
        { if(!valid)throw new InvalidOperationException(path+": "+message); }
        private static void Visit(Value a,Value b,Type type,string path,SnapshotComparison report)
        {
            Require(a.Kind==b.Kind,path,"JSON structure/type changed.");
            if(a.Kind=='0'){Require(!type.IsValueType,path,"Null cannot stand in for a required value type.");report.strictlyComparedValues++;return;}
            if(type==typeof(string)){Require(a.Kind=='s'&&a.Text==b.Text,path,"String/ID changed.");report.strictlyComparedValues++;return;}
            if(type==typeof(bool)){Require(a.Kind=='b'&&a.Text==b.Text,path,"Boolean changed.");report.strictlyComparedValues++;return;}
            if(type.IsEnum){Visit(a,b,Enum.GetUnderlyingType(type),path,report);return;}
            if(type==typeof(int)||type==typeof(long)||type==typeof(uint)||type==typeof(ulong)||type==typeof(short)||type==typeof(ushort)||type==typeof(byte)||type==typeof(sbyte))
            {
                Require(a.Kind=='n',path,"Integer is not a numeric JSON token.");
                // Decimal parses integer tokens exactly up to UInt64; disallow fractions/exponents entirely.
                decimal x=decimal.Parse(a.Text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture),y=decimal.Parse(b.Text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
                object ax=Convert.ChangeType(x,type,CultureInfo.InvariantCulture),by=Convert.ChangeType(y,type,CultureInfo.InvariantCulture);
                Require(ax.Equals(by),path,"Integer/enum/RNG value changed.");report.strictlyComparedValues++;return;
            }
            if(type==typeof(double))
            {
                Require(a.Kind=='n',path,"Double is not a numeric JSON token.");
                double x=double.Parse(a.Text,NumberStyles.Float,CultureInfo.InvariantCulture),y=double.Parse(b.Text,NumberStyles.Float,CultureInfo.InvariantCulture);
                Require(!double.IsNaN(x)&&!double.IsInfinity(x)&&!double.IsNaN(y)&&!double.IsInfinity(y),path,"Double is not finite.");
                double difference=Math.Abs(x-y),allowed=1e-12*Math.Max(1,Math.Max(Math.Abs(x),Math.Abs(y)));report.doubleValues++;
                if(difference>0)
                {
                    report.nonzeroDoubleDifferences.Add(new NumericRoundtripDifference{path=path,before=x,after=y,absoluteError=difference,allowedError=allowed});
                    if(difference>report.maxAbsoluteError){report.maxAbsoluteError=difference;report.maxErrorPath=path;}
                }
                Require(difference<=allowed,path,"Continuous double exceeds the declared codec tolerance.");return;
            }
            if(type.IsArray)
            {
                Require(a.Kind=='a'&&a.Elements.Count==b.Elements.Count,path,"Array shape/length changed.");
                for(int i=0;i<a.Elements.Count;i++)Visit(a.Elements[i],b.Elements[i],type.GetElementType(),path+"["+i+"]",report);return;
            }
            Require(a.Kind=='o',path,"Unsupported snapshot type "+type.FullName);
            var fields=new Dictionary<string,FieldInfo>(StringComparer.Ordinal);
            foreach(FieldInfo field in type.GetFields(BindingFlags.Instance|BindingFlags.Public))
                if(!field.IsNotSerialized)fields.Add(field.Name,field);
            Require(a.Fields.Count==fields.Count&&b.Fields.Count==fields.Count,path,"Serialized field set differs from the declared snapshot fields.");
            foreach(var field in fields)
            {
                Require(a.Fields.ContainsKey(field.Key)&&b.Fields.ContainsKey(field.Key),path+"."+field.Key,"Serialized field is missing.");
                Visit(a.Fields[field.Key],b.Fields[field.Key],field.Value.FieldType,path+"."+field.Key,report);
            }
        }
    }
}
#endif
