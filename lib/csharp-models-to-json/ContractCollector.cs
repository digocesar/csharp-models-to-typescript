using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpModelsToJson
{
    public class Contract
    {
        public string ContractName { get; set; }
        public IEnumerable<Operation> Operations { get; set; }
        public ExtraInfo ExtraInfo { get; set; }
        public string URLInfo { get; set; }

    }

    public class Operation
    {
        public string Identifier { get; set; }
        public string ReturnType { get; set; }
        public List<Parameter> Parameters { get; set; }

        public ExtraInfo ExtraInfo { get; set; }
    }

    public class Parameter
    {
        public string Identifier { get; set; }
        public string Type { get; set; }
    }

    public class ContractCollector : CSharpSyntaxWalker
    {
        public readonly List<Contract> Contracts = new List<Contract>();

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var contract = CreateContract(node);

            Contracts.Add(contract);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var contract = CreateContract(node);

            Contracts.Add(contract);
        }

        private static Contract CreateContract(TypeDeclarationSyntax node)
        {
            return new Contract()
            {
                ContractName = $"{node.Identifier.ToString()}{node.TypeParameterList?.ToString()}",
                Operations = ExtractOperations(node),

                ExtraInfo = new ExtraInfo
                {
                    Obsolete = Util.IsObsolete(node.AttributeLists),
                    ObsoleteMessage = Util.GetObsoleteMessage(node.AttributeLists),
                    Summary = Util.GetSummaryMessage(node),
                    Remarks = Util.GetRemarksMessage(node),
                },

                URLInfo = Util.GetContractURL(node)
            };
        }

        private static IEnumerable<Operation> ExtractOperations(TypeDeclarationSyntax node)
        {


            return node.Members.OfType<MethodDeclarationSyntax>()
                                            //.Where(method => IsAccessible(method.Modifiers))
                                            .Where(method => Util.IsContract(method.AttributeLists))
                                            .Select(ConvertMethod);
        }

        private static bool IsAccessible(SyntaxTokenList modifiers) => modifiers.All(modifier =>
            modifier.ToString() != "const" &&
            modifier.ToString() != "static" &&
            modifier.ToString() != "private"
        );

        private static Operation ConvertMethod(MethodDeclarationSyntax method) => new Operation
        {
            Identifier = method.Identifier.ToString(),
            ReturnType = method.ReturnType.ToString(),
            Parameters = method.ParameterList.Parameters.Select(parameter => new Parameter()
            {
                Identifier = parameter.Identifier.ToString(),
                Type = parameter.Type.ToString()
            }).ToList(),
            ExtraInfo = new ExtraInfo
            {
                Obsolete = Util.IsObsolete(method.AttributeLists),
                ObsoleteMessage = Util.GetObsoleteMessage(method.AttributeLists),
                Summary = Util.GetSummaryMessage(method),
                Remarks = Util.GetRemarksMessage(method),
            }

        };
    }
}