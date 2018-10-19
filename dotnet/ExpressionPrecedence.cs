using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;

namespace Compiler
{
    public delegate Expression ExpressionCreatorInfix(ParserToken token, Expression left, Expression right);

    public class ExpressionPrecedence
    {
        internal Dictionary<string, OperatorDefinition> precedence = new Dictionary<string, OperatorDefinition>();
        internal int highestLevel;

        public void AddOperatorDefinition(string mnemonic, int precedence, bool leftToRight, OperatorKind kind, ExpressionCreatorInfix handler)
        {
            if (precedence > highestLevel)
                highestLevel = precedence;
            this.precedence[operatorName(kind, mnemonic)] = new OperatorDefinition(precedence, leftToRight, kind, handler);
        }

        public ExpressionPrecedenceResolver CreateResolver()
        {
            return new ExpressionPrecedenceResolver(this);
        }

        internal static string operatorName(OperatorKind kind, string name)
        {
            switch (kind)
            {
                case OperatorKind.Prefix:
                    return name + "_prefix";
                case OperatorKind.Infix:
                    return name + "_infix";
                case OperatorKind.Postfix:
                    return name + "_postfix";
                default:
                    {
                        Require.NotCalled();
                        return null;
                    }
            }
        }
    }

    public class ExpressionPrecedenceResolver
    {
        private List<Opera_nd_tor> list = new List<Opera_nd_tor>();
        ExpressionPrecedence precedence;

        internal ExpressionPrecedenceResolver(ExpressionPrecedence precedence)
        {
            this.precedence = precedence;
        }

        public void AddOperator(ParserToken token, OperatorKind kind)
        {
            string op = ExpressionPrecedence.operatorName(kind, token.Keyword);
            if (!precedence.precedence.ContainsKey(op))
                throw new Exception("Unknown operator: "+op);
            list.Add(new Opera_nd_tor(precedence.precedence[op], token));
        }

        public void AddOperator(string token, Expression expression, OperatorKind kind)
        {
            list.Add(new Opera_nd_tor(precedence.precedence[ExpressionPrecedence.operatorName(kind, token)], expression));
        }

        public void AddOperand(Expression expression)
        {
            list.Add(new Opera_nd_tor(expression));
        }

        public Expression Reduce()
        {
            int level = 0;
            while (list.Count > 1)
            {
                Require.True(level <= precedence.highestLevel);
                // needs to be a for loop, the list gets modified in action
                //forward
                Opera_nd_tor item;
                for (int j = 0; j < 2; ++j)
                {
                    bool leftToRight = j == 0;
                    int i;
                    if (leftToRight)
                        i = -1;
                    else
                        i = list.Count;                        
                    while (true)
                    {
                        if (leftToRight)
                        {
                            i++;
                            if (i == list.Count)
                                break;
                        }
                        else
                        {
                            if (i == 0)
                                break;
                            i--;
                        }
                        item = list[i];
                        if (item.IsOperator && (item.Precedence == level) && (item.LeftToRight == leftToRight))
                        {
                            switch (item.Kind)
                            {
                                case OperatorKind.Infix:
                                    {
                                        i = i - 1;
                                        Expression left = list[i].Expression;
                                        Expression right = list[i + 2].Expression;
                                        list.RemoveRange(i, 3);
                                        list.Insert(i, item.ReduceInfix(left, right));
                                        break;
                                    }
                                case OperatorKind.Prefix:
                                    {
                                        Expression right = list[i + 1].Expression;
                                        list.RemoveRange(i, 2);
                                        list.Insert(i, item.ReducePrefix(right));
                                        break;
                                    }
                                case OperatorKind.Postfix:
                                    {
                                        i = i - 1;
                                        Expression left = list[i].Expression;
                                        list.RemoveRange(i, 2);
                                        list.Insert(i, item.ReducePostFix(left));
                                        break;
                                    }
                                default:
                                    {
                                        Require.NotCalled();
                                        break;
                                    }
                            }
                        }
                    }
                }

                level++;
            }
            Expression result = list[0].Expression;
            list.Clear();
            return result;
        }
    }

    public enum OperatorKind { Prefix, Infix, Postfix };

    class OperatorDefinition
    {
        private int precedence;
        private bool leftToRight;
        private OperatorKind kind;
        private ExpressionCreatorInfix handler;

        public int Precedence { get { return precedence; } }
        public bool LeftToRight { get { return leftToRight; } }
        public OperatorKind Kind { get { return kind; } }
        public ExpressionCreatorInfix Handler { get { return handler; } }

        public OperatorDefinition(int precedence, bool leftToRight, OperatorKind kind, ExpressionCreatorInfix handler)
        {
            this.precedence = precedence;
            this.leftToRight = leftToRight;
            this.kind = kind;
            this.handler = handler;
        }
    }

    class Opera_nd_tor
    {
        //operator
        private OperatorDefinition operatorDef;
        private ParserToken token;
        private Expression postfixExpression;

        //operand
        private Expression expression;

        public bool IsOperator { get { return operatorDef != null; } }
        public int Precedence { get { return operatorDef.Precedence; } }
        public bool LeftToRight { get { return operatorDef.LeftToRight; } }
        public OperatorKind Kind { get { return operatorDef.Kind; } }
        public Expression Expression { get { return expression; } }

        public Opera_nd_tor(OperatorDefinition operatorDef, ParserToken token)
        {
            Require.Assigned(operatorDef);
            this.operatorDef = operatorDef;
            this.token = token;
        }

        public Opera_nd_tor(OperatorDefinition operatorDef, Expression expression)
        {
            Require.Assigned(operatorDef);
            this.operatorDef = operatorDef;
            this.postfixExpression = expression;
        }

        public Opera_nd_tor(Expression expression)
        {
            this.expression = expression;
        }

        public Opera_nd_tor ReduceInfix(Expression left, Expression right)
        {
            return new Opera_nd_tor(operatorDef.Handler(token, left, right));
        }

        public Opera_nd_tor ReducePrefix(Expression right)
        {
            return new Opera_nd_tor(operatorDef.Handler(token, null, right));
        }

        public Opera_nd_tor ReducePostFix(Expression left)
        {
            return new Opera_nd_tor(operatorDef.Handler(null, left, postfixExpression));
        }
    }
}
