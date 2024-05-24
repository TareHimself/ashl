﻿namespace ashl.Parser;

/// <summary>
/// return <see cref="Expression"/>
/// </summary>
public class ReturnNode : Node
{
    public Node Expression;
    public ReturnNode(Node expression) : base(ENodeType.Return)
    {
        Expression = expression;
    }
}