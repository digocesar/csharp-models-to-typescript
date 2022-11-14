const path = require('path');
const camelcase = require('camelcase');

const flatten = arr => arr.reduce((a, b) => a.concat(b), []);

const arrayRegex = /^(.+)\[\]$/;
const simpleCollectionRegex = /^(?:I?List|IReadOnlyList|IEnumerable|ICollection|IReadOnlyCollection|HashSet)<([\w\d]+)>\??$/;
const collectionRegex = /^(?:I?List|IReadOnlyList|IEnumerable|ICollection|IReadOnlyCollection|HashSet)<(.+)>\??$/;
const simpleDictionaryRegex = /^(?:I?Dictionary|SortedDictionary|IReadOnlyDictionary)<([\w\d]+)\s*,\s*([\w\d]+)>\??$/;
const dictionaryRegex = /^(?:I?Dictionary|SortedDictionary|IReadOnlyDictionary)<([\w\d]+)\s*,\s*(.+)>\??$/;

const defaultTypeTranslations = {
    int: 'number',
    double: 'number',
    float: 'number',
    Int32: 'number',
    Int64: 'number',
    short: 'number',
    long: 'number',
    decimal: 'number',
    bool: 'boolean',
    DateTime: 'string',
    DateTimeOffset: 'string',
    Guid: 'string',
    dynamic: 'any',
    object: 'any',
    void: 'void'
};

const createConverter = config => {
    const typeTranslations = Object.assign({}, defaultTypeTranslations, config.customTypeTranslations);

    const convert = json => {
        const content = json.map(file => {
            const filename = path.relative(process.cwd(), file.FileName);

            const rows = flatten([
                ...file.Models.map(model => convertModel(model, filename)),
                ...file.Enums.map(enum_ => convertEnum(enum_, filename))
            ]);

            return rows
                .map(row => config.namespace ? `    ${row}` : row)
                .join('\n');
        });

        const filteredContent = content.filter(x => x.length > 0);

        if (config.namespace) {
            return [
                `declare module ${config.namespace} {`,
                ...filteredContent,
                '}',
            ].join('\n');
        } else {
            return filteredContent.join('\n');
        }
    };

    const convertContracts = json => {
        const content = json.map(file => {
            const filename = path.relative(process.cwd(), file.FileName);

            const rows = flatten([
                ...file.Contracts.map(contract => convertContract(contract, filename))
            ]);

            return rows
                .map(row => config.namespace ? `    ${row}` : row)
                .join('\n');
        });

        const filteredContent = content.filter(x => x.length > 0);

        if (config.namespace) {
            return [
                `declare module ${config.namespace} {`,
                ...filteredContent,
                '}',
            ].join('\n');
        } else {
            return filteredContent.join('\n');
        }
    };

    const convertModel = (model, filename) => {
        const rows = [];

        if (model.BaseClasses) {
            model.IndexSignature = model.BaseClasses.find(type => type.match(dictionaryRegex));
            model.BaseClasses = model.BaseClasses.filter(type => !type.match(dictionaryRegex));
        }

        const members = [...(model.Fields || []), ...(model.Properties || [])];
        const baseClasses = model.BaseClasses && model.BaseClasses.length ? ` extends ${model.BaseClasses.join(', ')}` : '';

        if (!config.omitFilePathComment) {
            rows.push(`// ${filename}`);
        }
        let classCommentRows = formatComment(model.ExtraInfo, '')
        if (classCommentRows) {
            rows.push(classCommentRows);
        }

        rows.push(`export interface ${model.ModelName}${baseClasses} {`);

        const propertySemicolon = config.omitSemicolon ? '' : ';';

        if (model.IndexSignature) {
            rows.push(`    ${convertIndexType(model.IndexSignature)}${propertySemicolon}`);
        }

        members.forEach(member => {
            let memberCommentRows = formatComment(member.ExtraInfo, '    ')
            if (memberCommentRows) {
                rows.push(memberCommentRows);
            }

            rows.push(`    ${convertProperty(member)}${propertySemicolon}`);
        });

        rows.push(`}\n`);

        return rows;
    };

    const convertContract = (contract, filename) => {
        const rows = [];

        const operations = [...(contract.Operations || [])];

        if (!config.omitFilePathComment) {
            rows.push(`// ${filename}`);
        }

        let classCommentRows = formatComment(contract.ExtraInfo, '')
        if (classCommentRows) {
            rows.push(classCommentRows);
        }

        rows.push(`export interface ${contract.ContractName} {`);

        operations.forEach(operation => {
            let operationCommentRows = formatComment(operation.ExtraInfo, '    ')
            if (operationCommentRows) {
                rows.push(operationCommentRows);
            }

            var methodSignature = (`    ${operation.Identifier}(`);

            for (let i = 0; i < operation.Parameters.length; i++) {

                let addComma = ', ';

                let parameter = operation.Parameters[i]

                if (i == operation.Parameters.length - 1) {
                    addComma = '';
                }

                methodSignature += (`${parameter.Identifier}: ${parseType(parameter.Type)}${addComma}`);
            }
            methodSignature += (`): ${parseType(operation.ReturnType)}`)

            rows.push(methodSignature);
            rows.push("");
        });
        rows.push(`}`);
        return rows;
    };

    const convertEnum = (enum_, filename) => {
        const rows = [];
        if (!config.omitFilePathComment) {
            rows.push(`// ${filename}`);
        }

        const entries = Object.entries(enum_.Values);

        let classCommentRows = formatComment(enum_.ExtraInfo, '')
        if (classCommentRows) {
            rows.push(classCommentRows);
        }

        const getEnumStringValue = (value) => config.camelCaseEnums
            ? camelcase(value)
            : value;

        const lastValueSemicolon = config.omitSemicolon ? '' : ';';

        if (config.stringLiteralTypesInsteadOfEnums) {
            rows.push(`export type ${enum_.Identifier} =`);

            entries.forEach(([i, entrie]) => {
                const delimiter = (Number(i) === entries.length - 1) ? lastValueSemicolon : ' |';
                rows.push(`    '${getEnumStringValue(entrie.Identifier)}'${delimiter}`);
            });

            rows.push('');
        } else {
            rows.push(`export enum ${enum_.Identifier} {`);

            entries.forEach(([i, entrie]) => {
                let classCommentRows = formatComment(entrie.ExtraInfo, '    ')
                if (classCommentRows) {
                    rows.push(classCommentRows);
                }
                if (config.numericEnums) {
                    rows.push(`    ${entrie.Identifier} = ${entrie.Value != null ? entrie.Value : i},`);
                } else {
                    rows.push(`    ${entrie.Identifier} = '${getEnumStringValue(entrie.Identifier)}',`);
                }
            });

            rows.push(`}\n`);
        }

        return rows;
    };

    const formatComment = (extraInfo, identation) => {
        if (!config.includeComments || !extraInfo || (!extraInfo.Obsolete && !extraInfo.Summary)) {
            return undefined;
        }

        let comment = '';
        comment += `${identation}/**\n`;

        if (extraInfo.Summary) {
            let commentLines = extraInfo.Summary.split(/\r?\n/);
            commentLines = commentLines.map((e) => {
                return `${identation} * ${replaceCommentTags(e)}\n`;
            })
            comment += commentLines.join('');
        }
        if (extraInfo.Remarks) {
            comment += `${identation} *\n`;
            comment += `${identation} * @remarks\n`;
            let commentLines = extraInfo.Remarks.split(/\r?\n/);
            commentLines = commentLines.map((e) => {
                return `${identation} * ${replaceCommentTags(e)}\n`;
            })
            comment += commentLines.join('');
        }

        if (extraInfo.Obsolete) {
            if (extraInfo.Summary) {
                comment += `${identation} *\n`;
            }

            let obsoleteMessage = '';
            if (extraInfo.ObsoleteMessage) {
                obsoleteMessage = ' ' + replaceCommentTags(extraInfo.ObsoleteMessage);
            }
            comment += `${identation} * @deprecated${obsoleteMessage}\n`;
        }

        comment += `${identation} */`;

        return comment;
    }

    const replaceCommentTags = comment => {
        return comment
            .replace(/<see cref="(.+)"\/>/gi, '{@link $1}')
            .replace(/<see cref="(.+)">(.+)<\/see>/gi, '{@link $1 | $2}')
            .replace('<inheritdoc/>', '@inheritDoc');
    }

    const convertProperty = property => {
        const optional = property.Type.endsWith('?');
        const identifier = convertIdentifier(optional ? `${property.Identifier.split(' ')[0]}?` : property.Identifier.split(' ')[0]);

        const type = parseType(property.Type);

        return `${identifier}: ${type}`;
    };

    const convertIndexType = indexType => {
        const dictionary = indexType.match(dictionaryRegex);
        const simpleDictionary = indexType.match(simpleDictionaryRegex);

        propType = simpleDictionary ? dictionary[2] : parseType(dictionary[2]);

        return `[key: ${convertType(dictionary[1])}]: ${convertType(propType)}`;
    };

    const convertRecord = indexType => {
        const dictionary = indexType.match(dictionaryRegex);
        const simpleDictionary = indexType.match(simpleDictionaryRegex);

        propType = simpleDictionary ? dictionary[2] : parseType(dictionary[2]);

        return `Record<${convertType(dictionary[1])}, ${convertType(propType)}>`;
    };

    const parseType = propType => {
        const array = propType.match(arrayRegex);
        if (array) {
            propType = array[1];
        }

        const collection = propType.match(collectionRegex);
        const dictionary = propType.match(dictionaryRegex);

        let type;

        if (collection) {
            const simpleCollection = propType.match(simpleCollectionRegex);
            propType = simpleCollection ? collection[1] : parseType(collection[1]);
            type = `${convertType(propType)}[]`;
        } else if (dictionary) {
            type = `${convertRecord(propType)}`;
        } else {
            const optional = propType.endsWith('?');
            type = convertType(optional ? propType.slice(0, propType.length - 1) : propType);
        }

        return array ? `${type}[]` : type;
    };

    const convertIdentifier = identifier => config.camelCase ? camelcase(identifier, config.camelCaseOptions) : identifier;
    const convertType = type => type in typeTranslations ? typeTranslations[type] : type;

    return [convert, convertContracts];
};

module.exports = createConverter;
