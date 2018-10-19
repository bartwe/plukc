using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Compiler.Metadata;
using System.Diagnostics.CodeAnalysis;

namespace Compiler
{
    class Syntax : IDisposable
    {
        private DefinitionCollection store;
        private Set<string> imports = new Set<string>();
        private Parser parser;
        private Dictionary<string, string> PrefixOperatorName = new Dictionary<string, string>();
        private Dictionary<string, string> InfixOperatorName = new Dictionary<string, string>();
        private ExpressionPrecedence expressionPrecedence = new ExpressionPrecedence();

        public Syntax(DefinitionCollection store, string sourceName, TextReader source)
        {
            this.store = store;
            Set<string> multiPartNonIdentifiers = new Set<string>(StringComparer.Ordinal);
            Set<string> keywords = new Set<string>(StringComparer.Ordinal);
            // multipart longer than 2 chars need to have their shorter preambles as valid entries too.
            multiPartNonIdentifiers.Add("==");
            multiPartNonIdentifiers.Add("!=");
            multiPartNonIdentifiers.Add("?=");
            multiPartNonIdentifiers.Add("||");
            multiPartNonIdentifiers.Add("&&");
            multiPartNonIdentifiers.Add("<");
            multiPartNonIdentifiers.Add(">");
            multiPartNonIdentifiers.Add("<<");
            multiPartNonIdentifiers.Add("<=");
            multiPartNonIdentifiers.Add(">=");
            multiPartNonIdentifiers.Add("..");
            multiPartNonIdentifiers.Add("=>");
            keywords.Add("true");
            keywords.Add("false");
            keywords.Add("null");
            keywords.Add("if");
            keywords.Add("else");
            keywords.Add("new");
            keywords.Add("return");
            keywords.Add("scope");
            keywords.Add("for");
            keywords.Add("while");
            keywords.Add("with");
            keywords.Add("throw");
            keywords.Add("try");
            keywords.Add("catch");
            keywords.Add("finally");
            keywords.Add("import");
            keywords.Add("class");
            keywords.Add("enum");
            keywords.Add("extern");
            keywords.Add("static");
            keywords.Add("abstract");
            keywords.Add("override");
            //            keywords.Add("explicit");
            keywords.Add("implicit");
            keywords.Add("public");
            keywords.Add("private");
            keywords.Add("protected");
            keywords.Add("internal");
            keywords.Add("this");
            keywords.Add("in");
            keywords.Add("asm");
            keywords.Add("cast");
            keywords.Add("get");
            keywords.Add("set");
            keywords.Add("var");
            keywords.Add("void");
            keywords.Add("dynamic");
            keywords.Add("int");
            keywords.Add("byte");
            keywords.Add("float");
            keywords.Add("bool");
            keywords.Add("string");
            keywords.Add("continue");
            keywords.Add("break");
            keywords.Add("recur");
            keywords.Add("is");
            keywords.Add("as");

            if (Program.SuppressPathInErrors)
            {
                int fs = sourceName.LastIndexOf("/");
                int bs = sourceName.LastIndexOf("\\");
                int s = Math.Max(fs, bs);
                if (s >= 0)
                    sourceName = sourceName.Substring(s+1);
            }

            parser = new Parser(sourceName, source, keywords, multiPartNonIdentifiers);
            PrefixOperatorName["+"] = "OperatorAcknowledge";
            PrefixOperatorName["-"] = "OperatorNegate";
            PrefixOperatorName["!"] = "OperatorNot";

            InfixOperatorName[">"] = "OperatorGreaterThan";
            InfixOperatorName["<"] = "OperatorLessThan";
            InfixOperatorName[">>"] = "OperatorRight";
            InfixOperatorName["<<"] = "OperatorLeft";
            InfixOperatorName[">="] = "OperatorGreaterEquals";
            InfixOperatorName["<="] = "OperatorLessEquals";
            InfixOperatorName["&&"] = "OperatorAnd";  // shortcut
            InfixOperatorName["||"] = "OperatorOr"; // shortcut
            InfixOperatorName["=="] = "OperatorEquals";
            InfixOperatorName["!="] = "OperatorNotEquals";
            InfixOperatorName[".."] = "OperatorRange";
            InfixOperatorName["+"] = "OperatorAdd";
            InfixOperatorName["-"] = "OperatorSubtract";
            InfixOperatorName["*"] = "OperatorMultiply";
            InfixOperatorName["/"] = "OperatorDivide";
            InfixOperatorName["%"] = "OperatorModulo";
            InfixOperatorName["&"] = "OperatorAnd";
            InfixOperatorName["|"] = "OperatorOr";
            InfixOperatorName["^"] = "OperatorXor";

            /*            expression start:
(!?-+ new null this <number> <string> <identifier>

expression tail:
.([= operators 
 */

            expressionPrecedence.AddOperatorDefinition(".", 2, true, OperatorKind.Postfix, PostfixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("(", 2, true, OperatorKind.Postfix, PostfixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("[", 2, true, OperatorKind.Postfix, PostfixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("{", 2, true, OperatorKind.Postfix, PostfixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("?", 2, true, OperatorKind.Postfix, PostfixExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("+", 4, false, OperatorKind.Prefix, PrefixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("-", 4, false, OperatorKind.Prefix, PrefixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("!", 4, false, OperatorKind.Prefix, PrefixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("~", 4, false, OperatorKind.Prefix, ForceAssignedExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("~~", 4, false, OperatorKind.Prefix, InferAssignedExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("?", 4, false, OperatorKind.Prefix, IsAssignedExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("*", 5, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("/", 5, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("%", 5, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("+", 6, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("-", 6, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("<<", 7, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition(">>", 7, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("<", 8, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("<=", 8, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition(">", 8, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition(">=", 8, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("is", 8, true, OperatorKind.Postfix, PostfixExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("as", 8, true, OperatorKind.Postfix, PostfixExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("==", 9, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("!=", 9, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("&", 10, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("^", 11, true, OperatorKind.Infix, InfixOperatorExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("|", 12, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("&&", 13, true, OperatorKind.Infix, AndExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("||", 14, true, OperatorKind.Infix, OrExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("??", 15, true, OperatorKind.Infix, IfAssignedExpressionHandler);
            expressionPrecedence.AddOperatorDefinition("..", 15, true, OperatorKind.Infix, InfixOperatorExpressionHandler);

            expressionPrecedence.AddOperatorDefinition("=", 16, false, OperatorKind.Infix, AssignmentExpressionHandler);
        }

        public string Parse()
        {
            while (Match("import"))
                ParseImport();
            string result = null;
            while (!Match("<EndOfStream>"))
            {
                Definition definition = ParseDeclaration();
                if (definition.IsTemplate)
                    store.AddTemplate(definition);
                else
                    store.Add(definition);
                if (result == null)
                    result = definition.Name.Data;
            }
            Consume("<EndOfStream>");
            return result;
        }

        private void ParseImport()
        {
            ILocation location = Consume("import");
            Identifier import = ParseIdentifier();
            if (imports.Contains(import.Data))
                throw new CompilerException(location, string.Format(Resource.Culture, Resource.DuplicateImport, import.Data));
            imports.Add(import.Data);
            Consume(";");
        }

        private Definition ParseDeclaration()
        {
            Modifiers modifiers = ParseModifiers();
            if (Match("class"))
                return ParseClass(modifiers);
            if (Match("enum"))
                return ParseEnum(modifiers);
            Fail();
            return null;
        }

        private Definition ParseEnum(Modifiers modifiers)
        {
            ILocation location = Consume("enum");
            Definition definition = new Definition(location, imports, modifiers);
            Identifier className = ParseIdentifier();
            imports.Put(className.Namespace);
            definition.SetName(className);
            TypeName baseType = new TypeName(new Identifier(location, "pluk.base.Enum"));
            TypeName selfType = definition.TypeReference.TypeName;
            baseType.AddTemplateParameter(selfType);
            definition.AddExtends(baseType);
            Consume("{");

            Modifiers m = new Modifiers(location);
            m.AddModifierPrivate(location);
            Parameters parameters = new Parameters();
            parameters.AddParameter(location, new TypeName(new Identifier(location, "pluk.base.Int")), new Identifier(location, "value"));
            parameters.AddParameter(location, new TypeName(new Identifier(location, "pluk.base.String")), new Identifier(location, "name"));
            Constructor c = new Constructor(location, m, parameters);
            List<Expression> bcel = new List<Expression>();
            bcel.Add(new SlotExpression(location, new Identifier(location, "value"), false));
            bcel.Add(new SlotExpression(location, new Identifier(location, "name"), false));
            c.CallBaseConstructor(baseType, bcel);
            c.SetBody(new EmptyStatement(location));
            definition.AddConstructor(c);


            int index = 1;

            while (true)
            {
                Identifier item = ParseShortIdentifier();
                Modifiers fieldModifiers = new Modifiers(item);
                fieldModifiers.AddModifierPrivate(item);
                fieldModifiers.AddModifierStatic(item);
                Identifier fieldName = new Identifier(item, "@" + item.Data);
                Field field = new Field(item, fieldModifiers, fieldModifiers, selfType, fieldName);
                definition.AddField(field);
                Expression initializer1 = new NewExpression(item, selfType);
                CallExpression initializer2 = new CallExpression(item, initializer1);
                initializer2.AddParameter(new NumberLiteralExpression(item, index));
                initializer2.AddParameter(new StringLiteralExpression(item, item.Data));
                definition.AddStaticInitializer(new ExpressionStatement(item, new AssignmentExpression(item, new SlotExpression(item, new Identifier(item, "this"), false), fieldName, initializer2)));

                Modifiers getModifiers = new Modifiers(item);
                Modifiers setModifiers = new Modifiers(item);
                getModifiers.AddModifierStatic(item);
                getModifiers.AddModifierPublic(item);
                setModifiers.AddModifierStatic(item);
                setModifiers.AddModifierPrivate(item);
                setModifiers.MakeUnusable();
                Property property = new Property(item, getModifiers, setModifiers, selfType, item, new ReturnStatement(item, new SlotExpression(item, fieldName, false)), null);
                definition.AddProperty(property);

                index++;
                if (!Match(","))
                    break;
                Consume(",");
            }

            Consume("}");
            definition.Complete();
            return definition;
        }

        private Definition ParseClass(Modifiers modifiers)
        {
            Definition definition;
            // annotation
            // modifiers
            ILocation location = Consume("class");
            definition = new Definition(location, imports, modifiers);
            Identifier className = ParseIdentifier();
            string ns = className.Namespace;
            while (true)
            {
                imports.Put(ns);
                ns = Identifier.ParentNamespace(ns);
                if (string.IsNullOrEmpty(ns))
                    break;
            }
            definition.SetName(className);
            if (Match("<"))
            {
                Consume("<");
                while (true)
                {
                    definition.AddTemplateParameter(ParseShortIdentifier());
                    if (Match(">")) break;
                    Consume(",");
                }
                Consume(">");
            }
            if (Match(":"))
            {
                Consume(":");
                while (true)
                {
                    definition.AddExtends(ParseType());
                    if (!Match(","))
                        break;
                    Consume(",");
                }
            }
            if (Match(";"))
            {
                Consume(";");
                definition.Complete();
                return definition;
            }
            Consume("{");
            while (!Match("}"))
            {
                ParseDocDeclaration();
                if (Match("implicit"))
                {
                    ILocation implLocation = Consume("implicit");
                    modifiers = new Modifiers(implLocation);
                    modifiers.AddModifierPublic(implLocation);
                    modifiers.AddModifierStatic(implLocation);
                    if (Match("extern"))
                        modifiers.AddModifierExtern(Consume("extern"));
                    TypeName typeName = ParseType();
                    Identifier name = ParseShortIdentifier();
                    Consume("(");
                    TypeName argumentTypeName = ParseType();
                    Identifier argumentName = ParseShortIdentifier();
                    Consume(")");
                    List<Identifier> methodTemplateParameters = new List<Identifier>();
                    Parameters parameters = new Parameters();
                    parameters.AddParameter(argumentTypeName, argumentTypeName, argumentName);
                    Statement body;
                    if (modifiers.Extern)
                        body = new EmptyStatement(Consume(";"));
                    else
                        body = ParseMethodBody();
                    definition.AddMethod(new Method(implLocation, modifiers, typeName, name, parameters, body, methodTemplateParameters, true));
                }
                else
                {
                    modifiers = ParseModifiers();
                    if (Match("{"))
                    {
                        Statement intializer = ParseStatementBlock();
                        if (modifiers.Static)
                            definition.AddStaticInitializer(intializer);
                        else
                            definition.AddInitializer(intializer);
                        continue;
                    }
                    if (Match("this"))
                    {
                        location = Consume("this");
                        Parameters parameters = ParseParameters();
                        Constructor constructor = new Constructor(location, modifiers, parameters);
                        if (Match(":"))
                        {
                            Consume(":");
                            if (Match("this"))
                            {
                                List<Expression> arguments = new List<Expression>();
                                ILocation otherLocation = Consume("this");
                                Consume("(");
                                if (!Match(")"))
                                    while (true)
                                    {
                                        Expression expression = ParseExpression();
                                        arguments.Add(expression);
                                        if (Match(")"))
                                            break;
                                        Consume(",");
                                    }
                                Consume(")");
                                constructor.CallAnotherConstructor(otherLocation, arguments);
                            }
                            else
                            {
                                while (true)
                                {
                                    TypeName type = null;
                                    if (!Match("("))
                                    {
                                        type = ParseTypeNoFunction();
                                    }
                                    List<Expression> arguments = new List<Expression>();
                                    Consume("(");
                                    if (!Match(")"))
                                        while (true)
                                        {
                                            Expression expression = ParseExpression();
                                            arguments.Add(expression);
                                            if (Match(")"))
                                                break;
                                            Consume(",");
                                        }
                                    Consume(")");
                                    constructor.CallBaseConstructor(type, arguments);
                                    if (!Match(","))
                                        break;
                                    Consume(",");
                                }
                            }
                        }
                        Statement body = ParseMethodBody();
                        constructor.SetBody(body);
                        definition.AddConstructor(constructor);
                    }
                    else
                    {
                        TypeName typeName = ParseType();
                        Identifier name = ParseShortIdentifier();
                        if (Match("=") || Match(",") || Match(";"))
                        {
                            modifiers.MakeDefaultPrivate();
                            while (true)
                            {
                                Expression initializer = null;
                                ILocation initLocation = null;
                                if (Match("="))
                                {
                                    initLocation = Consume("=");
                                    initializer = ParseExpression();
                                }
                                if (initializer != null)
                                {
                                    if (modifiers.Static)
                                        definition.AddStaticInitializer(new ExpressionStatement(initLocation, new AssignmentExpression(initLocation, new SlotExpression(initLocation, new Identifier(initLocation, "this"), false), name, initializer)));
                                    else
                                        definition.AddInitializer(new ExpressionStatement(initLocation, new AssignmentExpression(initLocation, new SlotExpression(initLocation, new Identifier(initLocation, "this"), false), name, initializer)));
                                }
                                definition.AddField(new Field(name, modifiers, modifiers, typeName, name));
                                if (Match(";"))
                                    break;
                                Consume(",");
                                name = ParseShortIdentifier();
                            }
                            Consume(";");
                        }
                        else if (Match("{"))
                        {
                            Consume("{");
                            Statement getStatement = null;
                            Statement setStatement = null;
                            Modifiers getModifiers = new Modifiers(modifiers);
                            Modifiers setModifiers = new Modifiers(modifiers);
                            bool busy = true;
                            bool noGet = true;
                            bool noSet = true;
                            bool implicitField = false;
                            while (busy)
                            {
                                Modifiers accessorMod = ParseModifiers();
                                busy = false;
                                if (Match("get") && (getStatement == null))
                                {
                                    getModifiers = new Modifiers(accessorMod, getModifiers);
                                    noGet = false;
                                    Consume("get");
                                    if (getModifiers.Abstract || implicitField)
                                        Consume(";");
                                    else
                                    {
                                        if (Match(";"))
                                        {
                                            Consume(";");
                                            implicitField = true;
                                        }
                                        else
                                            getStatement = ParseStatementBlock();
                                    }
                                    busy = true;
                                    continue;
                                }
                                else if (Match("set") && (setStatement == null))
                                {
                                    setModifiers = new Modifiers(accessorMod, setModifiers);
                                    noSet = false;
                                    Consume("set");
                                    if (setModifiers.Abstract || implicitField)
                                        Consume(";");
                                    else
                                    {
                                        if (Match(";"))
                                        {
                                            Consume(";");
                                            implicitField = true;
                                        }
                                        else
                                            setStatement = ParseStatementBlock();
                                    }
                                    busy = true;
                                    continue;
                                }
                            }
                            if (noGet)
                                getModifiers.MakeUnusable();
                            if (noSet)
                                setModifiers.MakeUnusable();
                            if (implicitField && (noGet || noSet))
                                throw new CompilerException(name, Resource.ImplicitFieldPropertyRequiresBothGetterAndSetter);

                            ILocation initLocation = null;
                            Expression initializer = null;
                            Consume("}");
                            if (implicitField && Match("="))
                            {
                                initLocation = Consume("=");
                                initializer = ParseExpression();
                                Consume(";");
                            }
                            if (initializer != null)
                            {
                                if (setModifiers.Static)
                                    definition.AddStaticInitializer(new ExpressionStatement(initLocation, new AssignmentExpression(initLocation, new SlotExpression(initLocation, new Identifier(initLocation, "this"), false), name, initializer)));
                                else
                                    definition.AddInitializer(new ExpressionStatement(initLocation, new AssignmentExpression(initLocation, new SlotExpression(initLocation, new Identifier(initLocation, "this"), false), name, initializer)));
                            }
                            if (implicitField)
                                definition.AddField(new Field(name, getModifiers, setModifiers, typeName, name));
                            else
                                definition.AddProperty(new Property(name, getModifiers, setModifiers, typeName, name, getStatement, setStatement));
                        }
                        else
                        {
                            List<Identifier> methodTemplateParameters = new List<Identifier>();
                            if (Match("<"))
                            {
                                Consume("<");
                                while (true)
                                {
                                    methodTemplateParameters.Add(ParseShortIdentifier());
                                    if (Match(">")) break;
                                    Consume(",");
                                }
                                Consume(">");
                            }
                            Parameters parameters = ParseParameters();
                            Statement body;

                            if (definition.Modifiers.Abstract && !(modifiers.Abstract || modifiers.Extern))
                            {
                                if (Match(";"))
                                    modifiers.AddModifierAbstract(name);
                            }
                            if (modifiers.Abstract || modifiers.Extern)
                                body = new EmptyStatement(Consume(";"));
                            else
                                body = ParseMethodBody();
                            definition.AddMethod(new Method(name, modifiers, typeName, name, parameters, body, methodTemplateParameters, false));
                        }
                    }
                }
            }
            Consume("}");
            return definition;
        }

        private Parameters ParseParameters()
        {
            Parameters parameters = new Parameters();
            Consume("(");
            if (!Match(")"))
                while (true)
                {
                    TypeName typename = ParseType();
                    Identifier name = ParseShortIdentifier();
                    parameters.AddParameter(typename, typename, name);
                    if (Match(")"))
                        break;
                    Consume(",");
                }
            Consume(")");
            return parameters;
        }

        private Statement ParseMethodBody()
        {
            if (Match("{"))
                return ParseStatementBlock();
            Fail();
            return null;
        }

        private Statement ParseStatement()
        {
            if (Match(";"))
                return new EmptyStatement(Consume(";"));
            if (Match("{"))
                return ParseStatementBlock();
            if (Match("return"))
                return ParseReturnStatement();
            if (Match("recur"))
                return ParseRecurStatement();
            if (Match("if"))
                return ParseIfStatement();
            if (Match("for"))
                return ParseForStatement();
            if (Match("with"))
                return ParseWithStatement();
            if (Match("while"))
                return ParseWhileStatement();
            if (Match("try"))
                return ParseTryStatement();
            if (Match("var"))
                return ParseVarVariableDefinitionStatement();
            if (Match("scope"))
                return ParseScopeStatement();
            if (Match("throw"))
                return ParseThrowStatement();
            if (Match("continue"))
                return ParseContinueStatement();
            if (Match("break"))
                return ParseBreakStatement();
            BeginLookahead();
            if (TryParseTypeNoFunction() && TryParseShortIdentifier())
            {
                RevertLookahead();
                return ParseVariableDefinitionStatement();
            }
            RevertLookahead();
            return ParseExpressionStatement();
        }

        private Statement ParseExpressionStatement()
        {
            Expression expession = ParseExpression();
            Consume(";");
            return new ExpressionStatement(expession, expession);
        }

        private Statement ParseVarVariableDefinitionStatement()
        {
            ILocation location = Consume("var");
            Identifier slot = ParseShortIdentifier();
            Consume("=");
            Expression expression = ParseExpression();
            Statement result = new VarAssignmentStatement(location, slot, expression);
            Consume(";");
            return result;
        }

        private Statement ParseVariableDefinitionStatement()
        {
            TypeName type = ParseTypeNoFunction();
            Identifier slot = ParseShortIdentifier();
            SlotStatement statement;
            CompoundStatement compound = null;
            while (true)
            {
                statement = new SlotStatement(type, type, slot);
                if (compound != null)
                    compound.Add(statement);
                Expression initializer;
                if (Match("="))
                {
                    Consume("=");
                    initializer = ParseExpression();
                    statement.AddStatement(new ExpressionStatement(slot, new AssignmentExpression(slot, null, slot, initializer)));
                }
                if (Match(";"))
                    break;
                if (compound == null)
                {
                    compound = new CompoundStatement(statement);
                    compound.Add(statement);
                }
                Consume(",");
                slot = ParseShortIdentifier();
            }
            Consume(";");
            if (compound == null)
                return statement;
            else
                return compound;
        }

        private Statement ParseIfStatement()
        {
            ILocation location = Consume("if");
            Consume("(");
            Expression expression = ParseExpression();
            Consume(")");
            Statement statement = ParseStatement();
            Statement elseStatment = null;
            if (Match("else"))
            {
                Consume("else");
                elseStatment = ParseStatement();
            }
            return new IfStatement(location, expression, statement, elseStatment);
        }

        private Statement ParseForStatement()
        {
            ILocation location = Consume("for");
            Consume("(");
            TypeName type = null;
            Identifier name = null;
            if (!Match("in"))
            {
                if (Match("var"))
                    Consume("var");
                else
                    type = ParseType();
                name = ParseShortIdentifier();
            }
            Consume("in");
            Expression expression = ParseExpression();
            Consume(")");
            Statement statement = ParseStatement();
            return new ForStatement(location, type, name, expression, statement);
        }

        private Statement ParseWithStatement()
        {
            ILocation location = Consume("with");
            Consume("(");
            Expression expression = ParseExpression();
            Consume(")");
            Statement statement = ParseStatement();
            return new WithStatement(location, expression, statement);
        }

        private Statement ParseWhileStatement()
        {
            ILocation location = Consume("while");
            Consume("(");
            Expression expression = ParseExpression();
            Consume(")");
            Statement statement = ParseStatement();
            return new WhileStatement(location, expression, statement);
        }

        private Statement ParseTryStatement()
        {
            ILocation location = Consume("try");
            Statement statement = ParseStatement();
            TryStatement result = new TryStatement(location, statement);
            //force atleast one of these
            if (!(Match("catch") || Match("finally")))
                Consume("catch");
            while (Match("catch"))
            {
                ILocation catchLocation = Consume("catch");
                Consume("(");
                TypeName type = ParseType();
                Identifier name = ParseShortIdentifier();
                Consume(")");
                Statement catchStatement = ParseStatement();
                result.AddCatchStatement(catchLocation, type, name, catchStatement);
            }
            if (Match("finally"))
            {
                ILocation finallyLocation = Consume("finally");
                result.SetFinallyStatement(finallyLocation, ParseStatement());
            }
            return result;
        }

        private Statement ParseReturnStatement()
        {
            ILocation location = Consume("return");
            Expression expression;
            if (Match(";"))
                expression = new NullExpression(location);
            else
                expression = ParseExpression();
            Consume(";");
            return new ReturnStatement(location, expression);
        }

        private Statement ParseRecurStatement()
        {
            ILocation location = Consume("recur");
            RecurStatement rs = new RecurStatement(location);
            if (Match("("))
            {
                Consume("(");
                if (!Match(")"))
                    while (true)
                    {
                        rs.AddParameter(ParseExpression());
                        if (Match(")"))
                            break;
                        Consume(",");
                    }
                Consume(")");
            }
            Consume(";");
            return rs;
        }

        private Statement ParseScopeStatement()
        {
            ILocation location = Consume("scope");
            Consume("(");
            TypeName type = null;
            if (Match("var"))
                Consume("var");
            else
                type = ParseType();
            Identifier name = ParseShortIdentifier();
            Consume("=");
            Expression expression = ParseExpression();
            Consume(")");
            Statement statement = ParseStatement();
            return new ScopeStatement(location, type, name, expression, statement);
        }

        private Statement ParseThrowStatement()
        {
            ILocation location = Consume("throw");
            Expression expression = ParseExpression();
            Consume(";");
            return new ThrowStatement(location, expression);
        }

        private Statement ParseContinueStatement()
        {
            ILocation location = Consume("continue");
            Consume(";");
            return new GotoStatement(location, "@continue");
        }

        private Statement ParseBreakStatement()
        {
            ILocation location = Consume("break");
            Consume(";");
            return new GotoStatement(location, "@break");
        }

        private Statement ParseStatementBlock()
        {
            BlockStatement block = new BlockStatement(Consume("{"));

            while (!Match("}"))
            {
                block.AddStatement(ParseStatement());
            }
            block.SetClosingLocation(Consume("}"));
            return block;
        }

        private Expression PostfixExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            Require.Unassigned(token);
            (right as PostfixExpression).SetParent(left);
            return right;
        }

        private Expression AssignmentExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            IAssignableExpression assignable = left as IAssignableExpression;
            if (assignable != null)
                return assignable.ConvertToAssignment(token, right);
            throw new CompilerException(token, string.Format(Resource.Culture, Resource.LeftHandSideCannotBeAssignedTo));
        }

        private Expression InfixOperatorExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            return new InfixOperatorExpression(token, left, right, token.Keyword, InfixOperatorName[token.Keyword]);
        }

        private Expression AndExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            return new AndExpression(token, left, right);
        }

        private Expression OrExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            return new OrExpression(token, left, right);
        }

        private Expression IsAssignedExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            Require.Unassigned(left);
            return new IsAssignedExpression(token, right);
        }

        private Expression ForceAssignedExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            Require.Unassigned(left);
            return new ForceAssignedExpression(token, right);
        }

        private Expression InferAssignedExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            Require.Unassigned(left);
            return new InferAssignedExpression(token, right);
        }

        private Expression PrefixExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            Require.Unassigned(left);
            return new PrefixOperatorExpression(token, right, token.Keyword, PrefixOperatorName[token.Keyword]);
        }

        private Expression IfAssignedExpressionHandler(ParserToken token, Expression left, Expression right)
        {
            return new IfAssignedExpression(token, left, right);
        }

        private Expression ParseExpression()
        {
            ExpressionPrecedenceResolver precedence = expressionPrecedence.CreateResolver();

            bool wantValue = true;
            while (true)
            {
                if (wantValue)
                {
                    wantValue = false;
                    BeginLookahead();
                    bool simpleLambda;
                    bool v = TryParseLambdaExpression(out simpleLambda);
                    RevertLookahead();
                    if (v)
                        precedence.AddOperand(ParseLambdaExpression(simpleLambda));
                    else if (Match("asm"))
                        precedence.AddOperand(ParseAsmExpression());
                    else if (Match("("))
                        precedence.AddOperand(ParseParenthesesExpression());
                    else if (Match("null"))
                        precedence.AddOperand(ParseNullExpression());
                    else if (Match("true") || Match("false"))
                        precedence.AddOperand(ParseBooleanExpression());
                    else if (Match("this"))
                        precedence.AddOperand(ParseThisExpression());
                    else if (Match("<Number>"))
                        precedence.AddOperand(ParseNumberLiteralExpression());
                    else if (Match("<String>"))
                        precedence.AddOperand(ParseStringLiteralExpression());
                    else if (Match("<Identifier>"))
                        precedence.AddOperand(ParseSlotExpression());
                    else if (Match("<"))
                        precedence.AddOperand(ParseTypeExpression());
                    else if (Match("int") || Match("byte") || Match("bool") || Match("string") || Match("float"))
                        precedence.AddOperand(ParseBuiltinTypeExpression());
                    else if (Match("new"))
                        precedence.AddOperand(ParseNewExpression());
                    else if (Match("cast"))
                        precedence.AddOperand(ParseCastExpression());
                    else if (Match("!") || Match("-") || Match("+") || Match("?") || Match("~"))
                    {
                        wantValue = true;
                        precedence.AddOperator(parser.Consume(), OperatorKind.Prefix);
                    }
                    else
                        Fail();
                }
                else
                {
                    if (Match("."))
                        precedence.AddOperator(".", ParseFieldExpression(), OperatorKind.Postfix);
                    else if (Match("("))
                        precedence.AddOperator("(", ParseCallExpression(), OperatorKind.Postfix);
                    else if (Match("["))
                        precedence.AddOperator("[", ParseIndexerExpression(), OperatorKind.Postfix);
                    else if (Match("{"))
                        precedence.AddOperator("{", ParseInitializerExpression(), OperatorKind.Postfix);
                    else if (Match("?"))
                        precedence.AddOperator("?", ParseTernaryExpression(), OperatorKind.Postfix);
                    else if (Match(">"))
                    {
                        bool shiftRight = false;
                        ParserToken token = parser.Consume();
                        if (Match(">"))
                        {
                            BeginLookahead();
                            ParserToken t = parser.Consume();
                            shiftRight = (t.Line == token.Line) && (t.Column == token.Column + 1);
                            RevertLookahead();
                            if (shiftRight)
                            {
                                Consume(">");
                                token = new ParserToken(token.Source, token.Line, token.Column, ">>", token.Kind);
                            }
                        }
                        wantValue = true;
                        precedence.AddOperator(token, OperatorKind.Infix);
                    }
                    else if (Match("as"))
                        precedence.AddOperator("as", ParseAsExpression(), OperatorKind.Postfix);
                    else if (Match("is"))
                        precedence.AddOperator("is", ParseIsExpression(), OperatorKind.Postfix);
                    else if (
(Match("+") || Match("-") || Match("*") || Match("/") || Match("~")
|| Match("&") || Match("|") || Match("$") || Match("%")
|| Match("^") || Match("@") || Match("!") || Match("<") || Match("==")
|| Match("..") || Match("<<") || Match("<=") || Match(">=") || Match("!=")
|| Match("||") || Match("&&") || Match("=")))
                    {
                        wantValue = true;
                        precedence.AddOperator(parser.Consume(), OperatorKind.Infix);
                    }
                    else
                        break;
                }
            }
            return precedence.Reduce();
        }

        private Expression ParseIsExpression()
        {
            ILocation location = Consume("is");
            TypeName type = ParseTypeNoFunction();
            return new IsTypeExpression(location, type);
        }

        private Expression ParseAsExpression()
        {
            ILocation location = Consume("as");
            TypeName type = ParseTypeNoFunction();
            return new AsTypeExpression(location, type);
        }

        private Expression ParseFieldExpression()
        {
            ILocation location = Consume(".");
            Identifier field = ParseShortIdentifier();
            return new FieldExpression(location, field);
        }

        private Expression ParseCallExpression()
        {
            ILocation location = Consume("(");
            CallExpression call = new CallExpression(location);
            if (!Match(")"))
                while (true)
                {
                    Expression expression = ParseExpression();
                    call.AddParameter(expression);
                    if (Match(")"))
                        break;
                    Consume(",");
                }
            Consume(")");
            return call;
        }

        private Expression ParseTernaryExpression()
        {
            ILocation qloc = Consume("?");
            Expression left = ParseExpression();
            Consume(":");
            Expression right = ParseExpression();
            return new TernaryExpression(qloc, left, right);
        }

        private Expression ParseIndexerExpression()
        {
            ILocation location = Consume("[");
            IndexorExpression result = new IndexorExpression(location);
            while (true)
            {
                Expression expression = ParseExpression();
                result.AddParameter(expression);
                if (Match("]"))
                    break;
                Consume(",");
            }
            Consume("]");
            return result;
        }

        private Expression ParseInitializerExpression()
        {
            ILocation location = Consume("{");
            InitializerExpression result = new InitializerExpression(location);
            while (true)
            {

                BeginLookahead();
                bool property = TryParseIdentifier() && Match("=");
                RevertLookahead();

                if (property)
                {
                    Identifier identifier = ParseIdentifier();
                    Consume("=");
                    Expression expression = ParseExpression();
                    result.AddFieldInitializer(identifier, expression);
                }
                else
                {
                    List<Expression> init = new List<Expression>();
                    if (Match("{"))
                    {
                        Consume("{");
                        while (true)
                        {
                            init.Add(ParseExpression());
                            if (Match("}"))
                                break;
                            Consume(",");
                        }
                        Consume("}");
                    }
                    else
                        init.Add(ParseExpression());
                    result.AddCollectionInitializer(init);
                }
                if (Match("}"))
                    break;
                Consume(",");
            }
            Consume("}");
            return result;
        }

        private Expression ParseLambdaExpression(bool simpleLambda)
        {
            List<Identifier> parameters = new List<Identifier>();
            List<TypeName> parameterTypes = new List<TypeName>();
            if (!Match("("))
            {
                Identifier arg = ParseShortIdentifier();
                parameters.Add(arg);
            }
            else
            {
                Consume("(");
                if (!Match(")"))
                    while (true)
                    {
                        if (simpleLambda)
                        {
                            Identifier arg = ParseShortIdentifier();
                            parameters.Add(arg);
                        }
                        else
                        {
                            TypeName type = ParseType();
                            parameterTypes.Add(type);
                            Identifier arg = ParseShortIdentifier();
                            parameters.Add(arg);
                        }
                        if (Match(")"))
                            break;
                        Consume(",");
                    }
                Consume(")");
            }
            ILocation location = Consume("=>");
            Statement statement;
            if (Match("{"))
                statement = ParseStatementBlock();
            else
                statement = new ReturnStatement(location, ParseExpression());
            if (simpleLambda)
                return new LambdaExpression(location, parameters, statement);
            else
                return new LambdaExpression(location, parameters, parameterTypes, statement);
        }

        private bool TryParseLambdaExpression(out bool simple)
        {
            simple = true;
            // x =>
            if (TryParseShortIdentifier())
                return Match("=>");

            // (..) =>
            if (!Match("("))
                return false;
            Consume("(");

            // () =>
            if (Match(")"))
            {
                Consume(")");
                return Match("=>");
            }

            // (x,y) => or (t x, t y) =>
            simple = TryParseShortIdentifier();
            if (!simple)
                if (!TryParseType())
                    return false;
            if (simple)
            {
                if (!(Match(")") || Match(",")))
                    simple = false;
            }
            if (!simple)
            {
                if (!TryParseShortIdentifier())
                    return false;
            }
            while (Match(","))
            {
                Consume(",");
                if (!simple)
                    if (!TryParseType())
                        return false;
                if (!TryParseShortIdentifier())
                    return false;
            }
            if (!Match(")"))
                return false;
            Consume(")");
            return Match("=>");
        }

        private Expression ParseAsmExpression()
        {
            ILocation location = Consume("asm");
            Consume("(");
            ParserToken text = Consume("<String>");
            Consume(")");
            return new AsmExpression(location, text);
        }

        private Expression ParseParenthesesExpression()
        {
            Consume("(");
            Expression expression = ParseExpression();
            Consume(")");
            return expression;
        }

        private Expression ParseNewExpression()
        {
            ILocation location = Consume("new");
            NewExpression result;
            if (Match("(") || Match(";") || Match(")") || Match(",") || Match("{") || Match("}"))
                result = new NewExpression(location);
            else
                result = new NewExpression(location, ParseTypeNoFunction());
            return result;
        }

        private Expression ParseCastExpression()
        {
            ILocation location = Consume("cast");
            Consume("<");
            TypeName type = ParseType();
            Consume(">");
            Consume("(");
            Expression expression = ParseExpression();
            Consume(")");
            return new CastExpression(location, type, expression);
        }

        private Expression ParseNullExpression()
        {
            ILocation location = Consume("null");
            return new NullExpression(location);
        }

        private Expression ParseBooleanExpression()
        {
            ILocation location;
            bool value;
            value = Match("true");
            if (value)
                location = Consume("true");
            else
                location = Consume("false");
            return new BooleanLiteralExpression(location, value);
        }

        private Expression ParseNumberLiteralExpression()
        {
            ParserToken number = Consume("<Number>");
            return new NumberLiteralExpression(number, number);
        }

        private Expression ParseStringLiteralExpression()
        {
            ParserToken text = Consume("<String>");
            return new StringLiteralExpression(text, text);
        }

        private Expression ParseSlotExpression()
        {
            Identifier identifier = ParseShortIdentifier();
            return new SlotExpression(identifier, identifier, false);
        }

        private Expression ParseTypeExpression()
        {
            ILocation location = Consume("<");
            Expression result = new TypeExpression(location, ParseType());
            Consume(">");
            return result;
        }

        private Expression ParseBuiltinTypeExpression()
        {
            TypeName typename = ParseType();
            Expression result = new TypeExpression(typename, typename);
            return result;
        }

        private Expression ParseThisExpression()
        {
            Identifier identifier = new Identifier(Consume("this"));
            return new SlotExpression(identifier, identifier, false);
        }

        private DocDeclaration ParseDocDeclaration()
        {
            if (Match("<String>"))
            {
                ParserToken token = Consume("<String>");
                return new DocDeclaration(token, token);
            }
            return null;
        }

        private Modifiers ParseModifiers()
        {
            Modifiers modifiers = new Modifiers(new NowhereLocation());
            while (true)
            {
                if (Match("public"))
                {
                    modifiers.AddModifierPublic(Consume("public"));
                    continue;
                }
                if (Match("protected"))
                {
                    modifiers.AddModifierProtected(Consume("protected"));
                    continue;
                }
                if (Match("private"))
                {
                    modifiers.AddModifierPrivate(Consume("private"));
                    continue;
                }
                if (Match("internal"))
                {
                    modifiers.AddModifierInternal(Consume("internal"));
                    continue;
                }
                if (Match("extern"))
                {
                    ILocation location = Consume("extern");
                    if (Match("asm"))
                    {
                        Consume("asm");
                        Extern externMetadata = new Extern(location);
                        modifiers.AddModifierExtern(location, externMetadata);
                    }
                    else if (Match("("))
                    {
                        Consume("(");
                        Extern externMetadata = new Extern(Consume("<String>"));
                        modifiers.AddModifierExtern(location, externMetadata);
                        Consume(")");
                    }
                    else
                        modifiers.AddModifierExtern(location);
                    continue;
                }
                if (Match("static"))
                {
                    modifiers.AddModifierStatic(Consume("static"));
                    continue;
                }
                if (Match("abstract"))
                {
                    modifiers.AddModifierAbstract(Consume("abstract"));
                    continue;
                }
                if (Match("override"))
                {
                    modifiers.AddModifierOverride(Consume("override"));
                    continue;
                }
                if (Match("final"))
                    Consume("final");
                break;
            }
            return modifiers;
        }

        private bool TryParseType()
        {
            if (!TryParseTypeNoFunction())
                return false;
            if (Match("("))
            {
                Consume("(");
                if (!Match(")"))
                    while (true)
                    {
                        if (!TryParseType())
                            return false;
                        if (Match(")"))
                            break;
                        if (!Match(","))
                            return false;
                        Consume(",");
                    }
                if (!Match(")"))
                    return false;
                Consume(")");
                if (Match("?"))
                    Consume("?");
                else if (Match("!"))
                    Consume("!");
            }
            return true;
        }

        private bool TryParseTypeNoFunction()
        {
            if (Match("<"))
            {
                Consume("<");
                if (!TryParseType())
                    return false;
                if (!Match(">"))
                    return false;
                Consume(">");
                return true;
            }
            if (!TryParseInnerTypeIterable())
                return false;
            if (Match("?"))
                Consume("?");
            else if (Match("!"))
                Consume("!");
            return true;
        }

        private bool TryParseInnerTypeName()
        {
            if (Match("void"))
                Consume("void");
            else if (Match("dynamic"))
                Consume("dynamic");
            else if (Match("int"))
                Consume("int");
            else if (Match("byte"))
                Consume("byte");
            else if (Match("bool"))
                Consume("bool");
            else if (Match("string"))
                Consume("string");
            else if (Match("float"))
                Consume("float");
            else if (!TryParseIdentifier())
                return false;
            return true;
        }

        private bool TryParseInnerTypeIterable()
        {
            if (Match("["))
            {
                Consume("[");
                if (!TryParseType())
                    return false;
                if (!Match("]"))
                    return false;
                Consume("]");
                return true;
            }
            else
                return TryParseInnerTypeArray();
        }

        private bool TryParseInnerTypeArray()
        {
            bool result = TryParseInnerTypeGeneric();
            if (Match("["))
            {
                Consume("[");
                if (!Match("]"))
                    return false;
                Consume("]");
                return true;
            }
            else
                return result;
        }

        private bool TryParseInnerTypeGeneric()
        {
            if (!TryParseInnerTypeName())
                return false;
            if (Match("<"))
            {
                Consume("<");
                while (true)
                {
                    if (!TryParseType())
                        return false;
                    if (Match(">")) break;
                    if (!Match(","))
                        return false;
                    Consume(",");
                }
                if (!Match(">"))
                    return false;
                Consume(">");
            }
            return true;
        }

        private TypeName ParseType()
        {
            TypeName typename = ParseTypeNoFunction();
            if (Match("("))
            {
                typename = typename.ConvertToFunction();
                Consume("(");
                if (!Match(")"))
                    while (true)
                    {
                        typename.AddFunctionParameter(ParseType());
                        if (Match(")"))
                            break;
                        Consume(",");
                    }
                Consume(")");
                if (Match("?"))
                {
                    Consume("?");
                    typename = new TypeName(typename, Nullability.ExplicitNullable);
                }
                else if (Match("!"))
                {
                    Consume("!");
                    typename = new TypeName(typename, Nullability.ExplicitNotNullable);
                }
            }
            return typename;
        }

        private TypeName ParseTypeNoFunction()
        {
            if (Match("<"))
            {
                Consume("<");
                TypeName tn = ParseType();
                Consume(">");
                return tn;
            }
            TypeName result = ParseInnerTypeIterable();
            if (Match("?"))
            {
                Consume("?");
                result = new TypeName(result, Nullability.ExplicitNullable);
            }
            else if (Match("!"))
            {
                Consume("!");
                result = new TypeName(result, Nullability.ExplicitNotNullable);
            }
            return result;
        }

        private TypeName ParseInnerTypeName()
        {
            if (Match("void"))
                return new TypeName(new Identifier(Consume("void")));
            else if (Match("dynamic"))
                return new TypeName(new Identifier(Consume("dynamic")));
            else if (Match("int"))
                return new TypeName(new Identifier(Consume("int"), "pluk.base.Int"), Nullability.NotNullable);
            else if (Match("byte"))
                return new TypeName(new Identifier(Consume("byte"), "pluk.base.Byte"), Nullability.NotNullable);
            else if (Match("bool"))
                return new TypeName(new Identifier(Consume("bool"), "pluk.base.Bool"), Nullability.NotNullable);
            else if (Match("string"))
                return new TypeName(new Identifier(Consume("string"), "pluk.base.String"), Nullability.NotNullable);
            else if (Match("float"))
                return new TypeName(new Identifier(Consume("float"), "pluk.base.Float"), Nullability.NotNullable);
            else
            {
                return new TypeName(ParseIdentifier());
            }
        }

        private TypeName ParseInnerTypeIterable()
        {
            if (Match("["))
            {
                ILocation location = Consume("[");
                TypeName iter = new TypeName(new Identifier(location, "pluk.base.Iterable"));
                iter.AddTemplateParameter(ParseType());
                Consume("]");
                return iter;
            }
            else
                return ParseInnerTypeArray();
        }

        private TypeName ParseInnerTypeArray()
        {
            TypeName result = ParseInnerTypeGeneric();
            if (Match("["))
            {
                ILocation location = Consume("[");
                Consume("]");
                TypeName arr = new TypeName(new Identifier(location, "pluk.base.Array"));
                arr.AddTemplateParameter(result);
                return arr;
            }
            else
                return result;
        }

        private TypeName ParseInnerTypeGeneric()
        {
            TypeName typename = ParseInnerTypeName();
            if (Match("<"))
            {
                Consume("<");
                while (true)
                {
                    typename.AddTemplateParameter(ParseType());
                    if (Match(">")) break;
                    Consume(",");
                }
                Consume(">");
            }
            return typename;
        }

        private bool TryParseShortIdentifier()
        {
            if (Match("<Identifier>"))
            {
                Consume("<Identifier>");
                return true;
            }
            return false;
        }

        private Identifier ParseShortIdentifier()
        {
            return new Identifier(Consume("<Identifier>"));
        }

        private bool TryParseIdentifier()
        {
            if (!TryParseShortIdentifier())
                return false;
            while (Match("."))
            {
                Consume(".");
                if (!TryParseShortIdentifier())
                    return false;
            }
            return true;
        }

        private Identifier ParseIdentifier()
        {
            ParserToken token = Consume("<Identifier>");
            if (!Match("."))
                return new Identifier(token, token.Token);
            StringBuilder sb = new StringBuilder();
            sb.Append(token.Token);
            while (true)
            {
                if (!Match("."))
                    break;
                sb.Append(Consume(".").Token);
                sb.Append(Consume("<Identifier>").Token);
            }
            return new Identifier(token, sb.ToString());
        }

        private bool Match(string keyword)
        {
            return parser.Match(keyword);
        }

        private ParserToken Consume(string keyword)
        {
            if (!Match(keyword))
                parser.Fail();
            return parser.Consume();
        }

        private void Fail()
        {
            parser.Fail();
        }

        private void BeginLookahead()
        {
            parser.BeginLookahead();
        }

        private void RevertLookahead()
        {
            parser.RevertLookahead();
        }

        #region IDisposable Members

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (parser != null)
                    parser.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
