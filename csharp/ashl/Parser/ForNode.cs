﻿namespace rsl.Parser;

public class ForNode : Node
{
    public Node Condition;
    public Node Initial;
    public ScopeNode Scope;
    public Node Update;

    public ForNode(Node initial, Node condition, Node update, ScopeNode scope) : base(ENodeType.For)
    {
        Initial = initial;
        Condition = condition;
        Update = update;
        Scope = scope;
    }

    public override IEnumerable<Node> GetChildren()
    {
        return [Initial, Condition, Update, Scope];
    }
}