using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Core.Arango.Linq.Internal
{
    
    public class AqlPrimitive : AqlConvertable
    {
        private readonly string _value;

        public string Value => _value;

        public AqlPrimitive(string value) : base(false)
        {
            _value = value;
        }


        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitPrimitive(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            return $"{_value}";
        }
    }

    public class AqlVariable : AqlConvertable
    {
        private readonly AqlQueryVariable _variable;

        public AqlVariable(AqlQueryVariable variable) : base(false)
        {
            _variable = variable;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitVariable(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            return $"{this._variable.Name}";
        }
    }
    
    public class AqlConstant : AqlConvertable
    {
        private readonly object _value;

        public object Value => _value;

        public string PreferredName => _preferredName;

        private readonly string _preferredName;

        public AqlConstant(object value, string preferredName) : base(false)
        {
            _value = value;
            _preferredName = preferredName;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VistConstant(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var label = bindVars.AddNewVar(_value, _preferredName);
            return $"@{label}";
        }
    }
    
    public class AqlParameter : AqlConvertable
    {
        public ParameterExpression Expr { get; }

        public AqlParameter(ParameterExpression expr) : base(false)
        {
            Expr = expr;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitParameter(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var paramName = Expr.Name;
            var o = parameters[paramName];
            return $"{o}";
        }
    }
    
    public class AqlMemberAccess : AqlConvertable
    {
        public AqlConvertable Left { get; }
        public string Member { get; }

        public AqlMemberAccess(AqlConvertable left, string member) : base(false)
        {
            Left = left;
            Member = member;
        }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj as AqlMemberAccess);
        }

        public bool Equals(AqlMemberAccess other)
        {
            return other != null
                   && this.Left == other.Left
                   && this.Member == other.Member;
        }
        

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitMemberAccess(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var l = Left.Convert(parameters, bindVars);
            return $"{l}.{Member}";
        }
    }
    
    public class AqlFunction : AqlConvertable
    {
        public string FunctionName { get; }
        public AqlConvertable[] Arguments { get; }

        public bool Equals(AqlFunction other)
        {
            return other != null
                   && this.FunctionName == other.FunctionName
                   && this.Arguments.SequenceEqual(other.Arguments);
        }

        public AqlFunction(string functionName, AqlConvertable[] arguments) : base(false)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitFunction(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var sb = new StringBuilder();

            var args = String.Join(", ", Arguments.Select(x => x.Convert(parameters, bindVars)));
            
            sb.AppendLine($"{FunctionName}({args})");

            return sb.ToString();
        }
    }

    public class AqlLength : AqlFunction
    {
        private readonly AqlCollection _collection;

        public AqlLength(AqlCollection collection) : base("LENGTH", new []{ collection })
        {
            _collection = collection;
        }
    }

    public class AqlInList : AqlConvertable
    {
        public AqlConvertable Element { get; }
        public AqlConvertable Container { get; }

        public AqlInList(AqlConvertable element, AqlConvertable container) : base(true)
        {
            Element = element;
            Container = container;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as AqlInList);
        }

        public bool Equals(AqlInList other)
        {
            return other != null
                   && this.Element == other.Element
                   && this.Container == other.Container;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitInList(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var c = Container.Convert(parameters, bindVars,true);
            var e = Element.Convert(parameters, bindVars, true);
            return $"{e} IN {c}";
        }
    }
    
    public class AqlBinary : AqlConvertable
    {

        public static Dictionary<ExpressionType, string> SupportedExpressionTypeOperators = new Dictionary<ExpressionType, string>()
        {
            {ExpressionType.And, "&&"},
            {ExpressionType.AndAlso, "&&"},
            {ExpressionType.Or, "||"},
            {ExpressionType.OrElse, "||"},
            {ExpressionType.Equal, "=="},
            {ExpressionType.GreaterThan, ">"},
            {ExpressionType.GreaterThanOrEqual, ">="},
            {ExpressionType.LessThan, "<"},
            {ExpressionType.LessThanOrEqual, "<="},
            {ExpressionType.Add, "+"}
        };

        public AqlConvertable Left { get; }
        public AqlConvertable Right { get; }
        public string OperatorStr { get; }

        public AqlBinary(AqlConvertable left, AqlConvertable right, string operatorStr) : base(true)
        {
            Left = left;
            Right = right;
            OperatorStr = operatorStr;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitBinary(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var l = Left.Convert(parameters, bindVars,true);
            var r = Right.Convert(parameters, bindVars, true);
            return $"{l} {OperatorStr} {r}";
        }
    }
    
    public class AqlUnary : AqlConvertable
    {

        public static Dictionary<ExpressionType, string> SupportedExpressionTypeOperators = new Dictionary<ExpressionType, string>()
        {
            {ExpressionType.Not, "!"},
        };

        public AqlConvertable Inner { get; }
        public string OperatorStr { get; }

        public AqlUnary(AqlConvertable inner, string operatorStr) : base(true)
        {
            Inner = inner;
            OperatorStr = operatorStr;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as AqlUnary);
        }

        public bool Equals(AqlUnary other)
        {
            return other != null
                   && this.Inner == other.Inner
                   && this.OperatorStr == other.OperatorStr;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitUnary(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var i = Inner.Convert(parameters, bindVars, true);
            return $"{OperatorStr} {i}";
        }
    }
}