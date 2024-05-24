﻿using System.Collections.Frozen;
using ashl.Tokenizer;

namespace ashl.Parser;

public class Parser
{
    public virtual TokenList<Token> ConsumeTill(TokenList<Token> input,int initialScope,params TokenType[] stopTokens)
    {
        var result = input.CreateEmpty();
        var scope = initialScope;
        var stopTokensSet = stopTokens.ToFrozenSet();
        while (input.NotEmpty())
        {
            switch (input.Front().Type)
            {
                case TokenType.OpenBrace or TokenType.OpenParen or TokenType.OpenBracket:
                    if (stopTokensSet.Contains(input.Front().Type) && scope == 0)
                    {
                        return result;
                    }
                    
                    scope++;
                    break;
                case TokenType.CloseBrace or TokenType.CloseParen or TokenType.CloseBracket:
                    if (stopTokensSet.Contains(input.Front().Type) && scope == 0)
                    {
                        return result;
                    }
                    
                    scope--;
                    break;
                default:
                    break;
            }
            
            if (stopTokensSet.Contains(input.Front().Type) && scope == 0)
            {
                return result;
            }
            
            result.InsertBack(input.RemoveFront());
        }

        return result;
    }


    public virtual IEnumerable<Node> ParseCallArguments(TokenList<Token> input)
    {
        var callTokens = ConsumeTill(input, 0, TokenType.CloseParen);
        input.ExpectFront(TokenType.CloseParen).RemoveFront();
        callTokens.ExpectFront(TokenType.OpenParen).RemoveFront();
        
        List<Node> arguments = new();
        
        while (callTokens.NotEmpty())
        {
            var argumentTokens = ConsumeTill(callTokens, 0, TokenType.Comma);

            if (callTokens.NotEmpty() && callTokens.Front().Type == TokenType.Comma)
            {
                callTokens.RemoveFront();
            }
            
            arguments.Add(ParseExpression(argumentTokens));
        }

        return arguments;
    }

    public virtual Node ResolveTokenToLiteralOrIdentifier(Token token)
    {
        var val = token.Value;
        if (int.TryParse(val, out var asInt))
        {
            return new IntLiteral(asInt);
        }
                
        if (float.TryParse(val, out var asFloat))
        {
            return new FloatLiteral(asFloat);
        }
                
        return new IdentifierNode(token.Value);
    }

    public virtual ArrayLiteralNode ParseArrayLiteral(TokenList<Token> input)
    {
        input.ExpectFront(TokenType.OpenBrace);
        var literalTokens = ConsumeTill(input, 0, TokenType.CloseParen);
        literalTokens.ExpectFront(TokenType.OpenBrace).RemoveFront();
        literalTokens.ExpectBack(TokenType.CloseBrace).RemoveBack();
        List<Node> expressions = new();
        while (literalTokens.NotEmpty())
        {
            var expression = ConsumeTill(literalTokens, 0, TokenType.Comma);
            expressions.Add(ParseExpression(expression));
            if (literalTokens.NotEmpty() && literalTokens.Front().Type == TokenType.Comma)
            {
                literalTokens.RemoveFront();
            }
        }

        return new ArrayLiteralNode(expressions);
    }
    public virtual Node ParsePrimary(TokenList<Token> input)
    {
        if (input.Empty())
        {
            input.ThrowExpectedInput();
        }

        switch (input.Front().Type)
        {
            case TokenType.Const:
                input.RemoveFront();
                return new ConstNode(ParseDeclaration(input));
            case TokenType.Identifier:
            {
                var front = input.RemoveFront();
                if (input.NotEmpty() && input.Front().Type is TokenType.Identifier) goto case TokenType.Unknown;
                return ResolveTokenToLiteralOrIdentifier(front);
            }
            break;
            case TokenType.Unknown:
            case TokenType.TypeFloat or TokenType.TypeInt or TokenType.TypeVec2f or TokenType.TypeVec2i
                or TokenType.TypeVec3f or TokenType.TypeVec3i or TokenType.TypeVec4f or TokenType.TypeVec4i
                or TokenType.TypeMat3 or TokenType.TypeMat4:
            {
                var targetToken = input.RemoveFront();
                
                if (input.NotEmpty() && input.Front().Type is TokenType.Unknown or TokenType.Identifier)
                {
                    input.InsertFront(targetToken);
                    return ParseDeclaration(input); // Good chance it is a declaration if the format is <unknown/type> <unknown/identifier>
                }

                return ResolveTokenToLiteralOrIdentifier(targetToken);
            }
                break;
            case TokenType.OpenParen:
            {
                var parenTokens = ConsumeTill(input, 0, TokenType.CloseParen);
                parenTokens.ExpectFront(TokenType.OpenParen).RemoveFront();
                input.ExpectFront(TokenType.CloseParen).RemoveFront();
                return new PrecedenceNode(ParseExpression(parenTokens));
            }
            case TokenType.OpenBrace:
            {
                return ParseArrayLiteral(input);
            }
                break;
            case TokenType.OpSubtract:
            {
                input.RemoveFront();
                return new NegateNode(ParsePrimary(input));
            }
            default:
                throw new Exception("Unknown Primary Token");
        }
    }
    
    public virtual Node ParseAccess(TokenList<Token> input)
    {
        var left = ParsePrimary(input);

        while (input.NotEmpty() && (input.Front().Type is TokenType.Access or TokenType.OpenBracket ||  (input.Front().Type is TokenType.OpenParen && left is IdentifierNode)))
        {
            
            switch (input.Front().Type)
            {
                case TokenType.OpenParen:
                {
                    if (left is IdentifierNode id)
                    {
                        left = new CallNode(id, ParseCallArguments(input));
                    }
                }
                    break;
                case TokenType.Access:
                {
                    input.RemoveFront();
                    left = new AccessNode(left, ParsePrimary(input));
                }
                    break;
                case TokenType.OpenBracket:
                {
                    input.RemoveFront();
                    var within = ConsumeTill(input, 0,TokenType.CloseBracket);
                    input.ExpectFront(TokenType.CloseBracket).RemoveFront();
                    left = new IndexNode(left, ParseExpression(within));
                }
                    break;
                
            }
        }

        return left;
    }
    
    public virtual Node ParseMultiplicative(TokenList<Token> input)
    {
        var left = ParseAccess(input);
        while (input.NotEmpty() && input.Front().Type is TokenType.OpMultiply or TokenType.OpDivide or TokenType.OpMod)
        {
            var token = input.RemoveFront();
            var right = ParseAccess(input);
            left = new BinaryOpNode(left, right,token.Type);
        }

        return left;
    }
    
    public virtual Node ParseAdditive(TokenList<Token> input)
    {
        var left = ParseMultiplicative(input);
        while (input.NotEmpty() && input.Front().Type is TokenType.OpAdd or TokenType.OpSubtract)
        {
            var token = input.RemoveFront();
            var right = ParseMultiplicative(input);
            left = new BinaryOpNode(left, right,token.Type);
        }

        return left;
    }
    
    
    public virtual Node ParseComparison(TokenList<Token> input)
    {
        var left = ParseAdditive(input);
        while (input.NotEmpty() && input.Front().Type is TokenType.OpEqual or TokenType.OpNotEqual or TokenType.OpLess or TokenType.OpLessEqual or TokenType.OpGreater or TokenType.OpGreaterEqual)
        {
            var token = input.RemoveFront();
            var right = ParseAdditive(input);
            left = new BinaryOpNode(left, right,token.Type);
        }

        return left;
    }
    
    
    public virtual Node ParseLogical(TokenList<Token> input)
    {
        var left = ParseComparison(input);
        while (input.NotEmpty() && input.Front().Type is TokenType.OpAnd or TokenType.OpOr or TokenType.OpNot)
        {
            var token = input.RemoveFront();
            var right = ParseComparison(input);
            left = new BinaryOpNode(left, right,token.Type);
        }

        return left;
    }
    
    public virtual Node ParseAssignment(TokenList<Token> input)
    {
        var left = ParseLogical(input);
        while (input.NotEmpty() && input.Front().Type == TokenType.Assign)
        {
            var tok = input.RemoveFront();
            var right = ParseLogical(input);
            left = new AssignNode(left, right);
        }

        return left;
    }
    
    public virtual Node ParseExpression(TokenList<Token> input)
    {
        return ParseAssignment(input);
    }
    
    public virtual Node ParseStatement(TokenList<Token> input)
    {
        var statementTokens = ConsumeTill(input, 0,TokenType.StatementEnd);
        input.ExpectFront(TokenType.StatementEnd).RemoveFront();
        
        switch (statementTokens.Front().Type)
        {
            case TokenType.Return:
            {
                statementTokens.RemoveFront();
                
                return new ReturnNode(ParseExpression(statementTokens));
            }
            break;
            default:
                return ParseExpression(statementTokens);
            break;
        }
        
        input.ThrowExpectedInput();
    }
    
    public virtual DeclarationNode ParseDeclaration(TokenList<Token> input)
    {
        var type = input.RemoveFront();
        var identifier = input.ExpectFront(TokenType.Identifier).RemoveFront();
        var count = input.ExpectFront(TokenType.DeclarationCount).RemoveFront();
        if (type.Type == TokenType.Identifier)
        {
            return new StructDeclarationNode(identifier.Value,int.Parse(count.Value),type.Value);
        }
        else
        {
            return new DeclarationNode(Utils.TokenTypeToDeclarationType(type.Type) ?? throw input.CreateException("Unexpected token",type),identifier.Value,int.Parse(count.Value));
        }
    }

    public virtual StructNode ParseStruct(TokenList<Token> input)
    {
        List<DeclarationNode> declarations = new();
        input.ExpectFront(TokenType.TypeStruct).RemoveFront();
        var structIdentifier = input.ExpectFront(TokenType.Identifier).RemoveFront();
        input.ExpectFront(TokenType.OpenBrace).RemoveFront();
        while (input.Front() is not { Type: TokenType.CloseBrace })
        {
            declarations.Add(ParseDeclaration(input));
            input.ExpectFront(TokenType.StatementEnd).RemoveFront();
        }

        input.ExpectFront(TokenType.CloseBrace).RemoveFront();
        return new StructNode(structIdentifier.Value, declarations);
    }

    public virtual LayoutNode ParseLayout(TokenList<Token> input)
    {
        input.ExpectFront(TokenType.Layout).RemoveFront();
        input.ExpectFront(TokenType.OpenParen).RemoveFront();

        var tags = new Dictionary<string, string>();
        
        while (input.Front().Type != TokenType.CloseParen)
        {
            tags.Add(input.ExpectFront(TokenType.Identifier).RemoveFront().Value,
                input.ExpectFront(TokenType.Identifier).RemoveFront().Value);
        }

        input.ExpectFront(TokenType.CloseParen).RemoveFront();

        var layoutType = input.RemoveFront().Type switch
        {
            TokenType.DataIn => ELayoutType.In,
            TokenType.DataOut => ELayoutType.Out,
            TokenType.Uniform => ELayoutType.Uniform,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var declaration = ParseDeclaration(input);
        input.ExpectFront(TokenType.StatementEnd).RemoveFront();

        return new LayoutNode(tags,layoutType,declaration);
    }


    public virtual NamedScopeNode ParseNamedScope(TokenList<Token> input)
    {
        var scopeType = input.ExpectFront(TokenType.VertexScope,TokenType.FragmentScope).RemoveFront().Type;
        input.ExpectFront(TokenType.OpenBrace).RemoveFront();
        
        List<Node> statements = new();
        while (input.Front() is not { Type: TokenType.CloseBrace })
        {
            switch (input.Front().Type)
            {
                case TokenType.Layout:
                    statements.Add(ParseLayout(input));
                    break;
                case TokenType.Function:
                    statements.Add(ParseFunction(input));
                    break;
                case TokenType.Const:
                {
                    statements.Add(ParseStatement(input));
                }
                    break;
                case TokenType.Include:
                {
                    statements.Add(ParseInclude(input));
                }
                    break;
                case TokenType.TypeStruct:
                {
                    statements.Add(ParseStruct(input));
                }
                    break;
                default:
                    throw input.CreateException("Unexpected Token", input.Front());
                    break;
            }
        }

        input.RemoveFront();

        return new NamedScopeNode(scopeType,statements);
    }

    public virtual ScopeNode ParseScope(TokenList<Token> input)
    {
        input.ExpectFront(TokenType.OpenBrace).RemoveFront();
        List<Node> statements = new();
        while (input.Front() is not { Type: TokenType.CloseBrace })
        {
            switch (input.Front().Type)
            {
                case TokenType.OpenParen:
                    statements.Add(ParseScope(input));
                    break;
                default:
                    statements.Add(ParseStatement(input));
                    break;
            }
        }

        input.RemoveFront();

        return new ScopeNode(statements);
    }

    public virtual FunctionNode ParseFunction(TokenList<Token> input)
    {
        var fnToken = input.ExpectFront(TokenType.Function).RemoveFront();
        var returnTokenType = Token.KeywordToTokenType(fnToken.Value) ?? TokenType.Identifier;
        var returnCount = input.ExpectFront(TokenType.DeclarationCount).RemoveFront();
        var fnName = input.ExpectFront(TokenType.Identifier).RemoveFront();
        input.ExpectFront(TokenType.OpenParen).RemoveFront();
        List<FunctionArgumentNode> arguments = new();
        
        while (input.Front() is not { Type: TokenType.CloseParen })
        {
            var isInput = true;
            if (input.Front().Type is TokenType.DataIn or TokenType.DataOut)
            {
                isInput = input.Front().Type == TokenType.DataIn;
                input.RemoveFront();
            }
            arguments.Add(new FunctionArgumentNode(isInput,ParseDeclaration(input)));
        }

        input.ExpectFront(TokenType.CloseParen).RemoveFront();
        
        var returnCountInt = int.Parse(returnCount.Value);
        
        var returnDeclaration = Utils.TokenTypeToDeclarationType(returnTokenType) is { } declType
            ? new DeclarationNode(declType,"", returnCountInt)
            : new StructDeclarationNode("", int.Parse(returnCount.Value),fnToken.Value);
        
        return new FunctionNode(fnName.Value,returnDeclaration,arguments, ParseScope(input));
    }

    public virtual IncludeNode ParseInclude(TokenList<Token> input)
    {
        var includeTok = input.ExpectFront(TokenType.Include).RemoveFront();
        var identifier = input.ExpectFront(TokenType.Identifier).RemoveFront();
        return new IncludeNode(includeTok.DebugInfo.File,identifier.Value);
    }

    public virtual ModuleNode Run(TokenList<Token> input)
    {
        if (input.Empty()) return new ModuleNode("",[]);
        List<Node> statements = new();
        var filePath = input.Front().DebugInfo.File;
        while (input.NotEmpty())
        {
            switch (input.Front().Type)
            {
                case TokenType.Include:
                {
                    statements.Add(ParseInclude(input));
                }
                    break;
                case TokenType.TypeStruct:
                {
                    statements.Add(ParseStruct(input));
                }
                    break;
                case TokenType.VertexScope or TokenType.FragmentScope:
                {
                    statements.Add(ParseNamedScope(input));
                }
                    break;
                case TokenType.Const:
                {
                    statements.Add(ParseStatement(input));
                }
                    break;
                case TokenType.Function:
                {
                    statements.Add(ParseFunction(input));
                }
                    break;
                default:
                    throw input.CreateException("Unexpected token", input.Front());
            }
        }

        return new ModuleNode(filePath,statements);
    }
}