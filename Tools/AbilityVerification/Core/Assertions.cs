using System;
using System.Collections;
using System.Collections.Generic;
namespace NUnit.Framework
{
 [AttributeUsage(AttributeTargets.Class)]public class TestFixtureAttribute:Attribute{}
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)]public class CategoryAttribute:Attribute{public CategoryAttribute(string name){}}
 [AttributeUsage(AttributeTargets.Method)]public class TestAttribute:Attribute{}
 [AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]public class TestCaseAttribute:Attribute{public object[] Arguments;public TestCaseAttribute(params object[] arguments){Arguments=arguments;}}
 public sealed class AssertionException:Exception{public AssertionException(string m):base(m){}}
 public static class Assert { public static void That(object actual, Constraint expected,string message=null){if(!expected.Matches(actual))throw new AssertionException((message??"Assertion failed")+": expected "+expected.Description+", actual "+(actual??"null"));} }
 public sealed class Constraint
 {
  private readonly Func<object,bool> predicate;private readonly object equal;private readonly bool isEqual;public string Description;
  public Constraint(string description,Func<object,bool> predicate){Description=description;this.predicate=predicate;}
  public Constraint(object equal){this.equal=equal;isEqual=true;Description="equal to "+equal;}
  public bool Matches(object a)=>isEqual?Equal(a,equal,0):predicate(a);
  public Constraint Within(double tolerance)=>new(Description+" within "+tolerance,a=>Equal(a,equal,tolerance));
  public static bool Equal(object a,object b,double tolerance)
  {
   if(a==null||b==null)return a==b;
   if(a is IConvertible&&b is IConvertible&&a is not string&&b is not string&&a is not bool&&b is not bool&&!a.GetType().IsEnum&&!b.GetType().IsEnum){try{return Math.Abs(Convert.ToDouble(a)-Convert.ToDouble(b))<=tolerance;}catch{}}
   if(a is IEnumerable aa&&b is IEnumerable bb&&a is not string&&b is not string){var ea=aa.GetEnumerator();var eb=bb.GetEnumerator();while(true){bool na=ea.MoveNext(),nb=eb.MoveNext();if(na!=nb)return false;if(!na)return true;if(!Equal(ea.Current,eb.Current,tolerance))return false;}}
   return a.Equals(b);
  }
 }
 public class ConstraintBuilder
 {
  private readonly bool invert;public ConstraintBuilder(bool invert=false){this.invert=invert;}
  Constraint Wrap(string label,Func<object,bool> p)=>new((invert?"not ":"")+label,a=>invert?!p(a):p(a));
  public ConstraintBuilder Not=>new(!invert);
  public Constraint True=>Wrap("true",a=>a is bool x&&x); public Constraint False=>Wrap("false",a=>a is bool x&&!x);
  public Constraint Null=>Wrap("null",a=>a==null);public Constraint Zero=>Wrap("zero",a=>Constraint.Equal(a,0,0));
  public Constraint Empty=>Wrap("empty",a=>a is string s?s.Length==0:a is IEnumerable e&&!e.GetEnumerator().MoveNext());
  public Constraint EqualTo(object expected)=>invert?Wrap("equal to "+expected,a=>Constraint.Equal(a,expected,0)):new Constraint(expected);
  public Constraint SameAs(object expected)=>Wrap("same reference",a=>ReferenceEquals(a,expected));
  public Constraint LessThanOrEqualTo(object expected)=>Wrap("<= "+expected,a=>Convert.ToDouble(a)<=Convert.ToDouble(expected));
 }
 public static class Is
 {
  static ConstraintBuilder B=>new();public static ConstraintBuilder Not=>B.Not;
  public static Constraint True=>B.True;public static Constraint False=>B.False;public static Constraint Null=>B.Null;public static Constraint Zero=>B.Zero;public static Constraint Empty=>B.Empty;
  public static Constraint EqualTo(object e)=>B.EqualTo(e);public static Constraint SameAs(object e)=>B.SameAs(e);public static Constraint LessThanOrEqualTo(object e)=>B.LessThanOrEqualTo(e);
 }
 public static class Does {public static Constraint Contain(string value)=>new("contain "+value,a=>a is string s&&s.Contains(value));}
}
