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
        

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            return $"{_value}";
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

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var label = bindVars.AddNewVar(_value, _preferredName);
            return $"@{label}";
        }
    }
    
    public class AqlParameter : AqlConvertable
    {
        private readonly ParameterExpression _expr;

        public AqlParameter(ParameterExpression expr) : base(false)
        {
            _expr = expr;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var paramName = _expr.Name;
            var o = parameters[paramName];
            return $"{o}";
        }
    }
    
    public class AqlMemberAccess : AqlConvertable
    {
        private readonly AqlConvertable _left;
        private readonly string _member;

        public AqlMemberAccess(AqlConvertable left, string member) : base(false)
        {
            _left = left;
            _member = member;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var l = _left.Convert(parameters, bindVars);
            return $"{l}.{_member}";
        }
    }
    
    public class AqlFunction : AqlConvertable
    {
        private readonly string _functionName;
        private readonly AqlConvertable[] _arguments;


        public AqlFunction(string functionName, AqlConvertable[] arguments) : base(false)
        {
            _functionName = functionName;
            _arguments = arguments;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var sb = new StringBuilder();

            var args = String.Join(", ", _arguments.Select(x => x.Convert(parameters, bindVars)));
            
            sb.AppendLine($"{_functionName}({args})");

            return sb.ToString();
        }
    }

    public class AqlLength : AqlConvertable
    {
        private readonly AqlCollection _collection;

        public AqlLength(AqlCollection collection) : base(false)
        {
            _collection = collection;
        }
        
        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"LENGTH({_collection.Convert(parameters, bindVars)})");

            return sb.ToString();
        }
    }

    public class AqlInList : AqlConvertable
    {
        private readonly AqlConvertable _element;
        private readonly AqlConvertable _container;

        public AqlInList(AqlConvertable element, AqlConvertable container) : base(true)
        {
            _element = element;
            _container = container;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var c = _container.Convert(parameters, bindVars,true);
            var e = _element.Convert(parameters, bindVars, true);
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
        
        private readonly AqlConvertable _left;
        private readonly AqlConvertable _right;
        private readonly string _operatorStr;

        public AqlBinary(AqlConvertable left, AqlConvertable right, string operatorStr) : base(true)
        {
            _left = left;
            _right = right;
            _operatorStr = operatorStr;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var l = _left.Convert(parameters, bindVars,true);
            var r = _right.Convert(parameters, bindVars, true);
            return $"{l} {_operatorStr} {r}";
        }
    }
    
    public class AqlUnary : AqlConvertable
    {

        public static Dictionary<ExpressionType, string> SupportedExpressionTypeOperators = new Dictionary<ExpressionType, string>()
        {
            {ExpressionType.Not, "!"},
        };
        
        private readonly AqlConvertable _inner;
        private readonly string _operatorStr;

        public AqlUnary(AqlConvertable inner, string operatorStr) : base(true)
        {
            _inner = inner;
            _operatorStr = operatorStr;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var i = _inner.Convert(parameters, bindVars, true);
            return $"{_operatorStr} {i}";
        }
    }
}