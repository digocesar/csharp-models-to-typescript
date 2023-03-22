using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CSharpModelsToJson.Tests
{
    [TestFixture]
    public class ModelCollectorTest
    {
        [Test]
        public void BasicInheritance_ReturnsInheritedClass()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public class A : B, C, D
                {
                    public void AMember()
                    {
                    }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            Assert.IsNotNull(modelCollector.Models);
            Assert.AreEqual(new[] { "B", "C", "D" }, modelCollector.Models.First().BaseClasses);
        }

        [Test]
        public void InterfaceImport_ReturnsSyntaxClassFromInterface()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public interface IPhoneNumber {
                    string Label { get; set; }
                    string Number { get; set; }
                    int MyProperty { get; set; }
                }

                public interface IPoint
                {
                   // Property signatures:
                   int x
                   {
                      get;
                      set;
                   }

                   int y
                   {
                      get;
                      set;
                   }
                }


                public class X {
                    public IPhoneNumber test { get; set; }
                    public IPoint test2 { get; set; }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.Visit(root);

            Assert.IsNotNull(modelCollector.Models);
            Assert.AreEqual(3, modelCollector.Models.Count);
            Assert.AreEqual(3, modelCollector.Models.First().Properties.Count());
        }


        [Test]
        public void TypedInheritance_ReturnsInheritance()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public class A : IController<Controller>
                {
                    public void AMember()
                    {
                    }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            Assert.IsNotNull(modelCollector.Models);
            Assert.AreEqual(new[] { "IController<Controller>" }, modelCollector.Models.First().BaseClasses);
        }

        [Test]
        public void AccessibilityRespected_ReturnsPublicOnly()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public class A : IController<Controller>
                {
                    const int A_Constant = 0;

                    private string B { get; set }

                    static string C { get; set }

                    public string Included { get; set }

                    public void AMember() 
                    { 
                    }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            Assert.IsNotNull(modelCollector.Models);
            Assert.IsNotNull(modelCollector.Models.First().Properties);
            Assert.AreEqual(1, modelCollector.Models.First().Properties.Count());
        }

        [Test]
        public void IgnoresJsonIgnored_ReturnsOnlyNotIgnored()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public class A : IController<Controller>
                {
                    const int A_Constant = 0;

                    private string B { get; set }

                    static string C { get; set }

                    public string Included { get; set }

                    [JsonIgnore]
                    public string Ignored { get; set; }

                    public void AMember() 
                    { 
                    }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            Assert.IsNotNull(modelCollector.Models);
            Assert.IsNotNull(modelCollector.Models.First().Properties);
            Assert.AreEqual(1, modelCollector.Models.First().Properties.Count());
        }

        [Test]
        public void DictionaryInheritance_ReturnsIndexAccessor()
        {
            var tree = CSharpSyntaxTree.ParseText(@"public class A : Dictionary<string, string> { }");

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            Assert.IsNotNull(modelCollector.Models);
            Assert.IsNotNull(modelCollector.Models.First().BaseClasses);
            Assert.AreEqual(new[] { "Dictionary<string, string>" }, modelCollector.Models.First().BaseClasses);
        }

        [Test]
        public void ReturnObsoleteClassInfo()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                [Obsolete(@""test"")]
                public class A
                {
                    [Obsolete(@""test prop"")]
                    public string A { get; set }

                    public string B { get; set }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.VisitClassDeclaration(root.DescendantNodes().OfType<ClassDeclarationSyntax>().First());

            var model = modelCollector.Models.First();

            Assert.IsNotNull(model);
            Assert.IsNotNull(model.Properties);

            Assert.IsTrue(model.ExtraInfo.Obsolete);
            Assert.AreEqual("test", model.ExtraInfo.ObsoleteMessage);

            Assert.IsTrue(model.Properties.First(x => x.Identifier.Equals("A")).ExtraInfo.Obsolete);
            Assert.AreEqual("test prop", model.Properties.First(x => x.Identifier.Equals("A")).ExtraInfo.ObsoleteMessage);

            Assert.IsFalse(model.Properties.First(x => x.Identifier.Equals("B")).ExtraInfo.Obsolete);
            Assert.IsNull(model.Properties.First(x => x.Identifier.Equals("B")).ExtraInfo.ObsoleteMessage);
        }

        [Test]
        public void ReturnObsoleteEnumInfo()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                [Obsolete(@""test"")]
                public enum A
                {
                    A = 0,
                    B = 1,
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var enumCollector = new EnumCollector();
            enumCollector.VisitEnumDeclaration(root.DescendantNodes().OfType<EnumDeclarationSyntax>().First());

            var model = enumCollector.Enums.First();

            Assert.IsNotNull(model);
            Assert.IsNotNull(model.Values);

            Assert.IsTrue(model.ExtraInfo.Obsolete);
            Assert.AreEqual("test", model.ExtraInfo.ObsoleteMessage);
        }

        [Test]
        public void ReturnEmmitDefaultValueInfo()
        {
            var tree = CSharpSyntaxTree.ParseText(@"
                public class A
                {
                    [DataMember(EmitDefaultValue = false)]
                    public bool Prop1 { get; set; }

                    [DataMember(EmitDefaultValue = true)]
                    public bool Prop2 { get; set; }

                    [DataMember( EmitDefaultValue = false )]
                    public bool Prop3 { get; set; }

                    [DataMember]
                    public bool Prop4 { get; set; }

                    public bool Prop5 { get; set; }
                }"
            );

            var root = (CompilationUnitSyntax)tree.GetRoot();

            var modelCollector = new ModelCollector();
            modelCollector.Visit(root);

            Assert.IsNotNull(modelCollector.Models);
            Assert.AreEqual(1, modelCollector.Models.Count);
            
            var properties = modelCollector.Models.First().Properties;

            Assert.IsFalse(properties.First(x => x.Identifier == "Prop1").ExtraInfo.EmitDefaultValue);
            Assert.IsTrue(properties.First(x => x.Identifier == "Prop2").ExtraInfo.EmitDefaultValue);
            Assert.IsFalse(properties.First(x => x.Identifier == "Prop3").ExtraInfo.EmitDefaultValue);
            Assert.IsTrue(properties.First(x => x.Identifier == "Prop4").ExtraInfo.EmitDefaultValue);
            Assert.IsTrue(properties.First(x => x.Identifier == "Prop5").ExtraInfo.EmitDefaultValue);
        }
    }
}