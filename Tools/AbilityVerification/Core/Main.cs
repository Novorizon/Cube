using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
class Entry
{
 static int Main(string[] args)
 {
  int pass=0,fail=0;
  Console.WriteLine("Portable source-test host: minimal NUnit assertion compatibility + Unity math/JSON substitutes; not Unity Test Runner.");
  foreach(var t in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.GetCustomAttribute<TestFixtureAttribute>()!=null).OrderBy(t=>t.FullName))
  foreach(var m in t.GetMethods().Where(m=>m.GetCustomAttribute<TestAttribute>()!=null||m.GetCustomAttributes<TestCaseAttribute>().Any()).OrderBy(m=>m.Name))
  {
   if(args.Length>0&&!t.FullName.Contains(args[0]))continue;
   var cases=m.GetCustomAttributes<TestCaseAttribute>().Select(x=>x.Arguments).ToArray(); if(cases.Length==0)cases=new[]{Array.Empty<object>()};
   foreach(var c in cases){string name=t.Name+"."+m.Name+"("+string.Join(",",c.Select(x=>x?.ToString()))+")";try{m.Invoke(Activator.CreateInstance(t),c);pass++;Console.WriteLine("PASS "+name);}catch(Exception e){fail++;Console.WriteLine("FAIL "+name+" : "+(e.InnerException??e));}}
  }
  Console.WriteLine($"RESULT {pass} passed, {fail} failed");return fail==0?0:1;
 }
}
