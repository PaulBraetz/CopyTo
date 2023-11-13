namespace TestApp.MyNS;

using RhoMicro.CopyToGenerator;

using System.Net.Sockets;

[GenerateCopyTo]
public partial class TestClass
{
    public TestClass TestClassProp { get; set; }
    public IEnumerable<TestClass> TestClassEnumerationProp { get; set; }
    public ICollection<TestClass> TestClassCollectionProp { get; set; }
    public String TestProp { get; set; }
}
