using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Ganss.IO;

namespace CSharpModelsToJson
{
    class File
    {
        public string FileName { get; set; }
        public IEnumerable<Model> Models { get; set; } = new List<Model>();
        public IEnumerable<Enum> Enums { get; set; } = new List<Enum>();
        public IEnumerable<Contract> Contracts { get; set; } = new List<Contract>();
    }

    class Program
    {
        static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile(args[0], true, true)
                .Build();

            List<string> includes = new List<string>();
            List<string> excludes = new List<string>();
            List<string> WCFContracts = new List<string>();

            config.Bind("include", includes);
            config.Bind("exclude", excludes);
            config.Bind("wcfContracts", WCFContracts);

            List<File> files = new List<File>();

            foreach (string fileName in getFileNames(includes, excludes))
            {
                files.Add(parseFile(fileName));
            }

            foreach (string fileName in getFileNames(WCFContracts, excludes))
            {
                files.Add(parseContractFile(fileName));
            }

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(files, Formatting.None, serializerSettings);

            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            System.Console.WriteLine(json);
            
        }

        static List<string> getFileNames(List<string> includes, List<string> excludes) {
            List<string> fileNames = new List<string>();

            foreach (var path in expandGlobPatterns(includes)) {
                fileNames.Add(path);
            }

            foreach (var path in expandGlobPatterns(excludes)) {
                fileNames.Remove(path);
            }

            return fileNames;
        }

        static List<string> expandGlobPatterns(List<string> globPatterns) {
            List<string> fileNames = new List<string>();

            foreach (string pattern in globPatterns) {
                var paths = Glob.Expand(pattern);

                foreach (var path in paths) {
                    fileNames.Add(path.FullName);
                }
            }

            return fileNames;
        }

        static File parseFile(string path) {
            string source = System.IO.File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            var root = (CompilationUnitSyntax) tree.GetRoot();
 
            var modelCollector = new ModelCollector();
            var enumCollector = new EnumCollector();

            modelCollector.Visit(root);
            enumCollector.Visit(root);

            return new File() {
                FileName = System.IO.Path.GetFullPath(path),
                Models = modelCollector.Models,
                Enums = enumCollector.Enums
            };
        }

        static File parseContractFile(string path)
        {
            string source = System.IO.File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var collector = new ContractCollector();

            collector.Visit(root);

            return new File()
            {
                FileName = System.IO.Path.GetFullPath(path),
                Contracts = collector.Contracts
            };
        }
    }
}