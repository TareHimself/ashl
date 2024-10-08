﻿namespace rsl.Parser;

/// <summary>
///     const <see cref="Declaration" />
/// </summary>
public class ConstNode : Node
{
    public DeclarationNode Declaration;

    public ConstNode(DeclarationNode declaration) : base(ENodeType.Const)
    {
        Declaration = declaration;
    }

    public override IEnumerable<Node> GetChildren()
    {
        return [Declaration];
    }
}