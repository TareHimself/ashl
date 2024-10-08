﻿namespace rsl.Parser;

public class DecrementNode : Node
{
    public bool IsPre;
    public Node Target;

    public DecrementNode(Node target, bool isPre) : base(ENodeType.Decrement)
    {
        Target = target;
        IsPre = isPre;
    }


    public override IEnumerable<Node> GetChildren() => [Target];
}