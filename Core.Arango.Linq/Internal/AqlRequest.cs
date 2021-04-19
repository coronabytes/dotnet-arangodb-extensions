﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Core.Arango.Linq.Internal.Util.Extensions;

namespace Core.Arango.Linq.Internal
{

    public abstract class AqlConvertable
    {
        private readonly bool _isCompound;

        public AqlConvertable(bool isCompound)
        {
            _isCompound = isCompound;
        }

        public string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars, bool bracketsAroundCompound)
        {
            var c = Convert(parameters, bindVars);

            if (bracketsAroundCompound && _isCompound)
            {
                return $"({c})";
            }

            return c;
        }
        
        public abstract string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars);
    }

    public class AqlSimpleSelect
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
    }
    
    public class AqlSort
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
    }
    
    public class AqlWhere
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
    }

    public class UnconvertableExpressionException : Exception
    {
        private readonly Expression _expr;

        public UnconvertableExpressionException(Expression expr): base("Unconvertable expression!" + expr.ToString())
        {
            _expr = expr;
        }
    }
    
    public class UnhandledExpressionException : Exception
    {
        private readonly Expression _expr;

        public UnhandledExpressionException(Expression expr): base("Unhandled expression!" + expr.ToString())
        {
            _expr = expr;
        }
    }
    
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

    public class AqlInnerExpressionVisitor : ExpressionVisitor
    {
        public static AqlConvertable Build(Expression expr)
        {
            var v = new AqlInnerExpressionVisitor();
            var result = v.Visit(expr);

            var c = v.getConvertable();
            if (c == null)
            {
                throw new UnconvertableExpressionException(expr);
            }
            
            return c;
        }
        
        private AqlInnerExpressionVisitor()
        {
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {

            if (node.Method.DeclaringType.IsGenericType && node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>))
            {
                switch (node.Method.Name)
                {
                    case "Contains":
                    {
                        var container = Build(node.Object);
                        var element = Build(node.Arguments[0]);
                        _aqlConvertable = new AqlInList(element, container);
                        return node;
                    }
                }
            } 
            else if (node.Method.DeclaringType == typeof(System.String))
            {
                switch (node.Method.Name)
                {
                    case "StartsWith":
                    {
                        var element = Build(node.Object);
                        var value = Build(node.Arguments[0]);
                        var like = new AqlConcat(value, new AqlConstant(@"%", "percent"), "startsWith");
                        _aqlConvertable = new AqlBinary(element, like, operatorStr: "LIKE");
                        return node;
                    }
                }
            }
            else if (node.Method.DeclaringType == typeof(System.DateTime))
            {
                switch (node.Method.Name)
                {
                    case "AddDays":
                    {
                        var date = Build(node.Object);
                        var amount = Build(node.Arguments[0]);
                        var unit = new AqlPrimitive(@"""days""");

                        _aqlConvertable = new AqlDateAdd(date, amount, unit);
                        return node;
                    }
                }
            }


            throw new UnhandledExpressionException(node);
        }

        private AqlConvertable AqlConcat(AqlConvertable value, AqlPrimitive aqlPrimitive)
        {
            throw new NotImplementedException();
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is int)
            {
                _aqlConvertable = new AqlPrimitive(node.Value.ToString());
                return node;
            }
            
            _aqlConvertable = new AqlConstant(node.Value.ToString(), "c");
            return node;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _aqlConvertable = new AqlParameter(node);
            return node;
        }


        protected override Expression VisitMember(MemberExpression node)
        {

            if (node.NodeType == ExpressionType.MemberAccess)
            {
                if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
                {
                    var left = Build(node.Expression);

                    var memberName = node.Member.Name;

                    if (memberName == "Key")
                    {
                        memberName = "_key";
                    }
                    
                    _aqlConvertable = new AqlMemberAccess(left, memberName);
                    return node;                    
                }
                else
                {
                    Expression<Func<object>> le = Expression.Lambda<Func<object>>(Expression.Convert(node, typeof(object)));
                    var compiledExpression = le.Compile();
                    var value = compiledExpression();
                    _aqlConvertable = new AqlConstant(value, node.Member.Name);
                    return node;
                }
                
            }

            
            throw new UnhandledExpressionException(node);
        }
        
        

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (AqlUnary.SupportedExpressionTypeOperators.TryGetValue(node.NodeType, out var operatorStr))
            {
                var body = Build(node.Operand);
                _aqlConvertable = new AqlUnary(body, operatorStr);
                return node;
            }
            
            throw new UnhandledExpressionException(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (AqlBinary.SupportedExpressionTypeOperators.TryGetValue(node.NodeType, out var operatorStr))
            {
                var left = Build(node.Left);
                var right = Build(node.Right);
                _aqlConvertable = new AqlBinary(left, right, operatorStr);
                return node;
            }

            throw new UnhandledExpressionException(node);
        }

        private AqlConvertable _aqlConvertable;

        AqlConvertable getConvertable()
        {
            return this._aqlConvertable;
        }
        
    }

    public class AqlDateAdd : AqlConvertable
    {
        private readonly AqlConvertable _date;
        private readonly AqlConvertable _amount;
        private readonly AqlConvertable _unit;

        public AqlDateAdd(AqlConvertable date, AqlConvertable amount, AqlConvertable unit) : base(false)
        {
            _date = date;
            _amount = amount;
            _unit = unit;
        }
        
        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            // DATE_ADD(DATE_NOW(), -1, "day")

            var d = _date.Convert(parameters, bindVars);
            var a = _amount.Convert(parameters, bindVars);
            var u = _unit.Convert(parameters, bindVars);
            
            return $"DATE_ADD({d}, {a}, {u})";
            throw new NotImplementedException();
        }

        
    }

    public class AqlConcat : AqlConvertable
    {
        private readonly AqlConvertable _left;
        private readonly AqlConvertable _right;
        private readonly string _preferredName;

        public AqlConcat(AqlConvertable left, AqlConvertable right, string preferredName) : base(false)
        {
            _left = left;
            _right = right;
            _preferredName = preferredName;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            bool TryGetStringFromStringConstant(AqlConvertable c, out string value)
            {
               
                if (c is AqlConstant)
                {
                    var v = ((AqlConstant) c).Value;
                    if (v is string)
                    {
                        value = (string) v;
                        return true;
                    }
                    
                }

                value = null;
                return false;
            }

            
            if (TryGetStringFromStringConstant(_left, out var l) &&
                TryGetStringFromStringConstant(_right, out var r))
            {
                var result = l + r;
        
                var c = new AqlConstant(result, _preferredName);
        
                return c.Convert(parameters, bindVars);

            }

            var cLeft = _left.Convert(parameters, bindVars);
            var cRight = _right.Convert(parameters, bindVars);

            return $"CONCAT({cLeft}, {cRight})";
            
        }
    }

    public class AqlReturnObjectProjection : AqlConvertable
    {


        private Dictionary<string, AqlConvertable> memberDict = new Dictionary<string, AqlConvertable>();
        
        public AqlReturnObjectProjection() : base(false)
        {
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var sb = new StringBuilder();

            sb.AppendLine("{");

            var enumerator = memberDict.GetEnumerator();


            var memberAssignments = memberDict.Select(x =>
            {
                var (member, valueConvertable) = x;
                var value = valueConvertable.Convert(parameters, bindVars);
                return $"{member}: {value}";
            });

            sb.AppendLine(String.Join(",\r\n", memberAssignments));

            sb.AppendLine("}");

            return sb.ToString();
        }

        public void AddMember(string member, AqlConvertable value)
        {
            memberDict.Add(member, value);
        }
    }

    public class AqlLinqExpressionVisitor : ExpressionVisitor
    {
        private readonly AqlRequest _request;
        private readonly bool _forbidFurtherSelects;


        protected internal AqlLinqExpressionVisitor(AqlRequest request, bool forbidFurtherSelects = false)
        {
            _request = request;
            _forbidFurtherSelects = forbidFurtherSelects;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {

            if (node.Method.DeclaringType == typeof(System.Linq.Queryable))
            {
                switch (node.Method.Name)
                {
                    case "Where":
                    case "SingleOrDefault":
                    {
                        var whereLambda = (LambdaExpression) (((UnaryExpression) node.Arguments[1]).Operand);
                        

                        var body = AqlInnerExpressionVisitor.Build(whereLambda.Body);
                        var parameter = whereLambda.Parameters[0];
                    
                        var whereBlock = new AqlWhere() {Body = body, Parameter = parameter};
                    
                        _request.AddFilterBlock(whereBlock);
                    
                        var visitor = new AqlLinqExpressionVisitor(_request, true);
                        visitor.Visit(node.Arguments[0]);
                        return node;
                        
                    
                        return node;
                    }
                    case "Select":
                    {
                        if (_forbidFurtherSelects)
                        {
                            throw new Exception("Further selects are not supported!");
                        }
                        
                        
                        var selectLambda = (LambdaExpression) (((UnaryExpression) node.Arguments[1]).Operand);
                        AqlConvertable body;
                        
                        
                        var parameter = selectLambda.Parameters[0];

                        if (selectLambda.Body.NodeType == ExpressionType.New)
                        {
                            var expr = (NewExpression) selectLambda.Body;

                            var proj = new AqlReturnObjectProjection();

                            for (var i = 0; i < expr.Members.Count; i++)
                            {
                                var member = expr.Members[i].Name;
                                var value = AqlInnerExpressionVisitor.Build(expr.Arguments[i]);
                                proj.AddMember(member, value);
                            }

                            body = proj;
                        }
                        else if (selectLambda.Body.NodeType == ExpressionType.MemberInit)
                        {
                            var init = (MemberInitExpression) selectLambda.Body;

                            var proj = new AqlReturnObjectProjection();

                            foreach (var binding in init.Bindings)
                            {
                                if (binding.BindingType == MemberBindingType.Assignment)
                                {
                                    var assignment = (MemberAssignment) binding;
                                    var member = assignment.Member.Name;
                                    var value = AqlInnerExpressionVisitor.Build(assignment.Expression);

                                    proj.AddMember(member, value);

                                } else
                                {
                                    throw new UnhandledExpressionException(init); 
                                }
                                

                                // proj.AddBinding(binding.i);
                            }
                            
                            

                            body = proj;
                        }
                        else
                        {
                            body = AqlInnerExpressionVisitor.Build(selectLambda.Body);
                        }
                        
                        var aqlSelect = new AqlSimpleSelect() { Body = body, Parameter = parameter};
                        _request.SetSelect(aqlSelect);
                        
                        
                        
                        var visitor = new AqlLinqExpressionVisitor(_request, true);
                        visitor.Visit(node.Arguments[0]);
                        return node;
                    }
                    case "Take":
                    {
                        
                        Expression<Func<int>> limitValueExpression = Expression.Lambda<Func<int>>(node.Arguments[1]);
                        var compiledExpression = limitValueExpression.Compile();
                        var limitValue = compiledExpression();
                        
                        
                        _request.SetLimit(limitValue);
                        
                        var visitor = new AqlLinqExpressionVisitor(_request, _forbidFurtherSelects);
                        visitor.Visit(node.Arguments[0]);
                        return node;
                    }
                    case "OrderBy":
                    {
                        
                        var orderByLambda = (LambdaExpression) (((UnaryExpression) node.Arguments[1]).Operand);
                        var body = AqlInnerExpressionVisitor.Build(orderByLambda.Body);
                        var parameter = orderByLambda.Parameters[0];
                        
                        var aqlSort = new AqlSort() { Body = body, Parameter = parameter};
                        
                        
                        _request.SetSort(aqlSort);
                        
                        var visitor = new AqlLinqExpressionVisitor(_request, _forbidFurtherSelects);
                        visitor.Visit(node.Arguments[0]);
                        return node;
                    }
                        
                }
            }
            
            throw new UnhandledExpressionException(node);
            // return base.VisitMethodCall(node);
        }
    }


    public class AqlBindVarsPool
    {
        
        private Dictionary<string,object> _dict = new Dictionary<string, object>();

        public string AddNewVar(object value, string preferredName = "p")
        {
            var name = GetNewFreeName(preferredName);
            _dict.Add(name, value);
            return name;
        }

        private string GetNewFreeName(string preferredName)
        {

            var i = -1;
            while (true)
            {
                var tryName = preferredName + (i < 0 ? "" : i.ToString());
                if (!_dict.ContainsKey(tryName))
                {
                    return tryName;
                }
                i++;
            }
        }

        public Dictionary<string, object> ToDict()
        {
            return _dict;
        }
    }
    
    public class AqlRequest
    {
        private AqlSimpleSelect _selectBlock = null;
        private int? _limit;
        private AqlSort _sortBlock = null;


        public List<AqlWhere> filterBlocks { get; set; } = new List<AqlWhere>();
        
        private AqlRequest()
        {
        }

        public static AqlRequest FromExpression(Expression expr, string collection)
        {
            var request = new AqlRequest();
            request.Collection = collection;
            
            var visitor = new AqlLinqExpressionVisitor(request);
            visitor.Visit(expr);
            

            return request;
        }

        public string Collection { get; set; }

        public (string, Dictionary<string ,object>) ToAqlQuery()
        {

            var bindVars = new AqlBindVarsPool();
            
            var projectionVarLabel = "x";


            var addBrackets = this.filterBlocks.Count > 1;

            
            var filters = filterBlocks
                .Select(x => 
                    x.Body.Convert(
                        new Dictionary<string, string>(){ {x.Parameter.Name, projectionVarLabel} },
                        bindVars
                        ));
            if (addBrackets)
                filters = filters.Select(x => $"({x})");
            
            
            var filterString = String.Join(" && ", filters);


            var sb = new StringBuilder();
            
            sb.AppendLine($"FOR {projectionVarLabel} IN {Collection}");
            if (filterString.Length > 0)
            {
                sb.AppendLine($"FILTER {filterString}");
            }

            if (_limit.HasValue)
            {
                sb.AppendLine($"LIMIT {_limit.Value}");
            }

            if (_sortBlock != null)
            {
                var sortString = _sortBlock.Body.Convert(
                    new Dictionary<string, string>(){ {_sortBlock.Parameter.Name, projectionVarLabel} },
                    bindVars
                );
                sb.AppendLine($"SORT {sortString}");
            }

            if (_selectBlock != null)
            {
                var selectString = _selectBlock.Body.Convert(
                    new Dictionary<string, string>(){ {_selectBlock.Parameter.Name, projectionVarLabel} },
                    bindVars
                    );
                sb.AppendLine($"RETURN {selectString}");
            }
            else
            {
                sb.AppendLine($"RETURN {projectionVarLabel}");
            }


            return (sb.ToString(), bindVars.ToDict());
        }

        public void AddFilterBlock(AqlWhere whereBlock)
        {
            this.filterBlocks.Add(whereBlock);
        }

        public void SetSelect(AqlSimpleSelect selectBlock)
        {
            this._selectBlock = selectBlock;
        }

        public void SetLimit(int i)
        {
            this._limit = i;
        }

        public void SetSort(AqlSort aqlSort)
        {
            this._sortBlock = aqlSort;
        }
    }
}