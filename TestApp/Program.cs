Console.WriteLine("Hello, World!");

var i1 = new TestClass() { TestProp = "Val 1" };
var i2 = new TestClass();
i1.CopyTo(i2);
Console.WriteLine(i2.TestProp);
