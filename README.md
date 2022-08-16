# C# models to TypeScript

This is a tool that consumes your C# domain models and types and creates TypeScript declaration files from them. There's other tools that does this but what makes this one different is that it internally uses [Roslyn (the .NET compiler platform)](https://github.com/dotnet/roslyn) to parse the source files, which removes the need to create and maintain our own parser.

This project is a fork from [svenheden/csharp-models-to-typescript](https://github.com/svenheden/csharp-models-to-typescript) and include some new features:
- Config option `omitSemicolon` to don't use semicolon at line ending.
- Config option `omitFilePathComment` to don't include comment with files path.
- Config option `includeComments` to include Comments, Remarks and Deprecated comments to TypeScript types.


[![NPM version][npm-image]][npm-url]


## Dependencies

* [.NET Core SDK](https://www.microsoft.com/net/download/macos)


## Install

```
$ npm install --save digocesar/csharp-models-to-typescript
```

## How to use

1. Add a config file to your project that contains for example...

```
{
    "include": [
        "./models/**/*.cs",
        "./enums/**/*.cs"
    ],
    "exclude": [
        "./models/foo/bar.cs"
    ],
    "namespace": "Api",
    "output": "./api.d.ts",
    "includeComments": true,
    "camelCase": false,
    "camelCaseEnums": false,
    "camelCaseOptions": {
        "pascalCase": false,
        "preserveConsecutiveUppercase": false,
        "locale": "en-US"
    },
    "numericEnums": false,
    "omitFilePathComment": false,
    "omitSemicolon": false,
    "stringLiteralTypesInsteadOfEnums": false,
    "customTypeTranslations": {
        "ProductName": "string",
        "ProductNumber": "string"
    }
}
```

2. Add a npm script to your package.json that references your config file...

```
"scripts": {
    "generate-types": "csharp-models-to-typescript --config=your-config-file.json"
},
```

3. Run the npm script `generate-types` and the output file specified in your config should be created and populated with your models.


## License

MIT © [Jonathan Svenheden](https://github.com/svenheden)

[npm-image]: https://img.shields.io/npm/v/csharp-models-to-typescript.svg
[npm-url]: https://npmjs.org/package/csharp-models-to-typescript
