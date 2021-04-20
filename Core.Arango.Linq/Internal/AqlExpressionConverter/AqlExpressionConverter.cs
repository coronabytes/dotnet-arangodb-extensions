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
    
    public class AqlFilter
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


    public class AqlQuery
    {
        private readonly AqlCollection _collection;

        public AqlQuery(AqlCollection collection)
        {
            _collection = collection;
        }

        public (string, Dictionary<string, object>, AqlQueryOutputBehaviour outputBehaviour) Compile()
        {
            var bindVars = new AqlBindVarsPool();

            var param = Expression.Parameter(typeof(object), "x");
            
            _collection.SetParameter(param);
            
            var paramDict = new Dictionary<string, string>();
            
            var compiledQuery = _collection.Convert(paramDict, bindVars);

            return (compiledQuery, bindVars.ToDict(), _collection.OutputBehaviour);
        }
        
    }
    
    public class AqlExpressionConverter
    {
        public static AqlConvertable ParseTerm(Expression expr)
        {
            var v = new AqlInnerExpressionVisitor();
            var result = v.Visit(expr);

            var c = v.getConvertable();
            
            return c;
        }

        public static AqlQuery ParseQuery(Expression queryExpression, string collection)
        {
            var query = new AqlCollection(false);
            
            var visitor = new AqlCollectionExpressionParser(query, true);
            visitor.Parse(queryExpression);
            query.Collection = new AqlPrimitive(collection);

            return new AqlQuery(query);
        }

        public static AqlCollection ParseCollection(Expression collectionExpression)
        {
            
            var request = new AqlCollection();

            var visitor = new AqlCollectionExpressionParser(request);
            visitor.Parse(collectionExpression);
            

            return request;
        }
    }

    

    public class AqlInnerExpressionVisitor : ExpressionVisitor
    {
        internal AqlInnerExpressionVisitor()
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
                        var container = AqlExpressionConverter.ParseTerm(node.Object);
                        var element = AqlExpressionConverter.ParseTerm(node.Arguments[0]);
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
                        var element = AqlExpressionConverter.ParseTerm(node.Object);
                        var value = AqlExpressionConverter.ParseTerm(node.Arguments[0]);
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
                        var date = AqlExpressionConverter.ParseTerm(node.Object);
                        var amount = AqlExpressionConverter.ParseTerm(node.Arguments[0]);
                        var unit = new AqlPrimitive(@"""days""");

                        _aqlConvertable = new AqlDateAdd(date, amount, unit);
                        return node;
                    }
                }
            }
            else if (node.Method.DeclaringType == typeof(System.Linq.Enumerable))
            {
                switch (node.Method.Name)
                {
                    case "Any":
                    {

                        var collectionExpression = node.Arguments[0];

                        var hasFilter = node.Arguments.Count > 1;
                        var predicateExpression = (LambdaExpression) node.Arguments[1];
                        var parameter = predicateExpression.Parameters[0];

                        var collection = AqlExpressionConverter.ParseCollection(collectionExpression);
                        collection.SetParameter(parameter);

                        var filterBody = AqlExpressionConverter.ParseTerm(predicateExpression.Body);
                        var filterBlock = new AqlFilter() {Body = filterBody, Parameter = parameter};
                        
                        collection.AddFilterBlock(filterBlock);
                        
                        
                        
                        var selectBody = new AqlPrimitive("true");
                        collection.SetSelect(new AqlSimpleSelect() { Body = selectBody, Parameter = parameter});

                        var length = new AqlLength(collection);
                        var aqlAny = new AqlBinary(length, new AqlPrimitive("0"), ">");

                        _aqlConvertable = aqlAny;
                        
                        
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
                    var left = AqlExpressionConverter.ParseTerm(node.Expression);
                    if (node.Expression.Type == typeof(string))
                    {
                        if (node.Member.Name == "Length")
                        {
                            _aqlConvertable = new AqlFunction("CHAR_LENGTH", new [] {left});
                            return node;
                        }
                    }
                    
                    

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
                var body = AqlExpressionConverter.ParseTerm(node.Operand);
                _aqlConvertable = new AqlUnary(body, operatorStr);
                return node;
            }
            
            throw new UnhandledExpressionException(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Add && node.Type == typeof(string))
            {
                var left = AqlExpressionConverter.ParseTerm(node.Left);
                var right = AqlExpressionConverter.ParseTerm(node.Right);
                _aqlConvertable = new AqlConcat(left, right, "strAdd");
                return node;
            }
            else if (AqlBinary.SupportedExpressionTypeOperators.TryGetValue(node.NodeType, out var operatorStr))
            {
                var left = AqlExpressionConverter.ParseTerm(node.Left);
                var right = AqlExpressionConverter.ParseTerm(node.Right);
                _aqlConvertable = new AqlBinary(left, right, operatorStr);
                return node;
            }

            throw new UnhandledExpressionException(node);
        }

        private AqlConvertable _aqlConvertable;

        public AqlConvertable getConvertable()
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
}