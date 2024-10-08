﻿namespace rsl.Parser;

/// <summary>
///     <see cref="HasLeftNode.Left" />[<see cref="IndexExpression" />]
/// </summary>
public class IndexNode : HasLeftNode
{
    public Node IndexExpression;

    public IndexNode(Node left, Node indexExpression) : base(left, ENodeType.Index)
    {
        IndexExpression = indexExpression;
    }

    public override IEnumerable<Node> GetChildren()
    {
        return [Left, IndexExpression];
    }
    
}