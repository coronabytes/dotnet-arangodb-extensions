﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Arango.Linq.Internal
{

    public abstract class AqlConvertable
    {
        private readonly bool _isCompound;


        public abstract AqlConvertable Accept(AqlVisitor visitor);
        
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

    public class AqlSimpleSelect : AqlParseQueryContextBuildStackElement
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeSelect(this);
        }

        public bool Equals(AqlSimpleSelect other)
        {
            return other != null
                   && this.Body == other.Body
                   && this.Parameter == other.Parameter;
        }
    }

    public class AqlOutputBehaviour : AqlParseQueryContextBuildStackElement
    {
        public AqlQueryOutputBehaviour Behaviour { get; }

        public AqlOutputBehaviour(AqlQueryOutputBehaviour behaviour)
        {
            Behaviour = behaviour;
        }

        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeOutputBehaviour(this);
        }
    }

    public class AqlLimit : AqlParseQueryContextBuildStackElement
    {
        public int Limit { get; }

        public AqlLimit(int limit)
        {
            Limit = limit;
        }

        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeLimit(this);
        }
    }
    
    public class AqlSort : AqlParseQueryContextBuildStackElement
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
        
        public bool Equals(AqlSort other)
        {
            return other != null
                   && this.Body == other.Body
                   && this.Parameter == other.Parameter;
        }

        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeSort(this);
        }
    }
    
    public class AqlFilter : AqlParseQueryContextBuildStackElement
    {
        public AqlConvertable Body { get; set; }
        public ParameterExpression Parameter { get; set; }
        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeFilter(this);
        }
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
        private Dictionary<AqlQueryVariable, AqlConvertable> _definitions = new Dictionary<AqlQueryVariable, AqlConvertable>();

        public AqlQuery(AqlCollection collection)
        {
            _collection = collection;
        }
        
        public void AddDefinition(AqlQueryVariable variable, AqlConvertable definitionConvertable)
        {
            _definitions.Add(variable, definitionConvertable);
        }

        public (string, Dictionary<string, object>, AqlQueryOutputBehaviour outputBehaviour) Compile()
        {
            var bindVars = new AqlBindVarsPool();

            var param = Expression.Parameter(typeof(object), "x");
            _collection.SetParameter(param);
            
            var paramDict = new Dictionary<string, string>();
            

            var sb = new StringBuilder();

            foreach (var ( variable, convertable) in _definitions)
            {
                var compiledValue = convertable.Convert(new Dictionary<string, string>(), bindVars);
                sb.AppendLine($"LET {variable.Name} = {compiledValue}");
            }
            
            
            var compiledCollection = _collection.Convert(paramDict, bindVars);
            sb.AppendLine(compiledCollection);

            var compiledQuery = sb.ToString();
            
            return (compiledQuery, bindVars.ToDict(), _collection.OutputBehaviour);
        }

        
    }

    public class AqlQueryVariable
    {
        public string Name { get; set; } 
    }

    public interface AqlParseQueryContextBuildStackElement
    {
        public void FeedToConsumer(BuildStackConsumer consumer);
    }

    public interface BuildStackConsumer
    {
        void ConsumeFilter(AqlFilter filter);
        void ConsumeSelect(AqlSimpleSelect aqlSimpleSelect);
        void ConsumeGrouping(AqlGrouping aqlGrouping);
        void ConsumeSort(AqlSort aqlSort);
        void ConsumeLimit(AqlLimit aqlLimit);
        void ConsumeOutputBehaviour(AqlOutputBehaviour aqlOutputBehaviour);
    }
    
    public class AqlParseQueryContext
    {

        private Dictionary<IQueryable, AqlQueryVariable> definitionDict = new Dictionary<IQueryable, AqlQueryVariable>();
        
        private Dictionary<string, AqlQueryVariable> _variables = new Dictionary<string, AqlQueryVariable>();


        private Regex rgx = new Regex("[^a-zA-Z0-9_]", RegexOptions.Compiled);

        public LinkedList<AqlParseQueryContextBuildStackElement> BuildStackElements { get; } =
            new LinkedList<AqlParseQueryContextBuildStackElement>();

        public void AddElementToBuildStack(AqlParseQueryContextBuildStackElement element)
        {
            BuildStackElements.AddFirst(element);
        }

        public void ConsumeBuildStack(BuildStackConsumer consumer)
        {
            foreach (var element in BuildStackElements)
            {
                element.FeedToConsumer(consumer);
            }
            BuildStackElements.Clear();
        }
        
        private string GetLegalVariableName(string name)
        {
            var newName = rgx.Replace(name, "");
            if (newName.Length > 128)
            {
                newName = newName.Substring(0, 128);
            }

            if (newName.Length == 0)
            {
                newName = "p";
            }
            
            return newName;
        }
        
        public AqlQueryVariable MakeNewVariable(string preferredName)
        {
            var baseName = GetLegalVariableName(preferredName);
            var suffix = "";
            var i = 0;

            while (true)
            {
                var combinedName = baseName + suffix;
                if (!_variables.ContainsKey(combinedName))
                {
                    var var = new AqlQueryVariable() { Name = combinedName};
                    _variables.Add(var.Name, var);
                    return var;
                }
                
                suffix = i.ToString();
                i++;
            }
        }
        
        /// <summary>
        /// Creates a new definition for a queryable or returns an existing one if already present for the queryable
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public AqlQueryVariable GetQueryiableDefinitionParameter(IQueryable queryable, string name)
        {
            if (definitionDict.TryGetValue(queryable, out var parameter))
            {
                return parameter;
            }

            var p = MakeNewVariable(name);
            definitionDict.Add(queryable, p);
            return p;
        }

        public Dictionary<IQueryable, AqlQueryVariable> GetDefinitions()
        {
            return definitionDict;
        }
    }
    
    public class AqlExpressionConverter
    {
        public static AqlConvertable ParseTerm(Expression expr, AqlParseQueryContext context)
        {
            var v = new AqlInnerExpressionVisitor(context);
            var result = v.Visit(expr);

            var c = v.getConvertable();
            
            return c;
        }

        public static AqlQuery ParseQuery(Expression queryExpression, string collection)
        {
            // var queryCollection = new AqlCollection(false);
            var context = new AqlParseQueryContext();
            
            var visitor = new AqlCollectionExpressionParser(context,true);
            var term = visitor.Parse(queryExpression);

            var query = new AqlQuery(term);
            
            var defs = context.GetDefinitions();
            
            foreach (var (queryable, variable) in defs)
            {
                var definitionConvertable = ParseDefinition(queryable.Expression, context);
                query.AddDefinition(variable, definitionConvertable);
            }

            return query;
        }

        private static AqlConvertable ParseDefinition(Expression expression, AqlParseQueryContext context)
        {
            ArangoQueryableContext<object> c = null;

            var visitor = new AqlCollectionExpressionParser(context,true);
            var collection = visitor.Parse(expression);
            
            var param = Expression.Parameter(typeof(object), "x");
            collection.SetParameter(param);
            collection.WithBrackets = true;


            return collection;
        }

        public static AqlCollection ParseCollection(Expression collectionExpression, AqlParseQueryContext context)
        {

            var oldStack = context.BuildStackElements.ToList();
            context.BuildStackElements.Clear();
            
            // var request = new AqlCollection();

            var visitor = new AqlCollectionExpressionParser(context, true);
            var request = visitor.Parse(collectionExpression);
            // request.WithBrackets = true;

            oldStack.Reverse();
            foreach (var element in oldStack)
            {
                context.AddElementToBuildStack(element);
            }


            return request;
        }
    }

    

    public class AqlInnerExpressionVisitor : ExpressionVisitor
    {
        private readonly AqlParseQueryContext _context;

        internal AqlInnerExpressionVisitor(AqlParseQueryContext context)
        {
            _context = context;
        }
        
        

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {

            
            Expression UnquoteExpression(Expression expr)
            {
                if (expr.NodeType == ExpressionType.Quote)
                {
                    var q = (UnaryExpression) expr;
                    return q.Operand;
                }

                return expr;
            }


            var aqlFunction = node.Method.GetCustomAttribute<AqlFunctionAttribute>();

            if (aqlFunction != null)
            {
                var args = node.Arguments.Select(x => AqlExpressionConverter.ParseTerm(x, _context));
                _aqlConvertable = new AqlFunction(aqlFunction.Name, args.ToArray());
                return node;
            }
            else if (node.Method.DeclaringType.IsGenericType && node.Method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>))
            {
                switch (node.Method.Name)
                {
                    case "Contains":
                    {
                        var container = AqlExpressionConverter.ParseTerm(node.Object, _context);
                        var element = AqlExpressionConverter.ParseTerm(node.Arguments[0], _context);
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
                        var element = AqlExpressionConverter.ParseTerm(node.Object, _context);
                        var value = AqlExpressionConverter.ParseTerm(node.Arguments[0], _context);
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
                        var date = AqlExpressionConverter.ParseTerm(node.Object, _context);
                        var amount = AqlExpressionConverter.ParseTerm(node.Arguments[0], _context);
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
                    case "Count":
                    {
                        var inner = node.Arguments[0];

                        var term = AqlExpressionConverter.ParseTerm(inner, _context);
                        
                        var length = new AqlFunction("LENGTH", new []{ term });


                        _aqlConvertable = length;
                        
                        return node;
                    }
                    case "ToList":
                    {
                        var inner = node.Arguments[0];
                        return this.Visit(inner);
                    }
                    case "Any":
                    {

                        var collectionExpression = node.Arguments[0];

                        var hasFilter = node.Arguments.Count > 1;
                        var predicateExpression = (LambdaExpression) node.Arguments[1];
                        var parameter = predicateExpression.Parameters[0];

                        var collection = AqlExpressionConverter.ParseCollection(collectionExpression, _context);
                        collection.SetParameter(parameter);

                        var filterBody = AqlExpressionConverter.ParseTerm(predicateExpression.Body, _context);
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
            else if (node.Method.DeclaringType == typeof(System.Linq.Queryable))
            {
                switch (node.Method.Name)
                {
                    case "Any":
                    {
                        
                        var collectionExpression = node.Arguments[0];

                        var hasFilter = node.Arguments.Count > 1;
                        var predicateExpression = (LambdaExpression) UnquoteExpression(node.Arguments[1]);
                        var parameter = predicateExpression.Parameters[0];
                        
                        var collection = AqlExpressionConverter.ParseCollection(collectionExpression, _context);
                        
                        collection.SetParameter(parameter);

                        var filterBody = AqlExpressionConverter.ParseTerm(predicateExpression.Body, _context);
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
            
            if (node.Type.GetInterfaces().Contains(typeof(IArangoQueryableCollection)))
            {
                var f = Expression.Lambda( node ).Compile();
                var value = f.DynamicInvoke();
                    
                var c = (IArangoQueryableCollection) value;

                var collectionName = c.Collection;
                
                _aqlConvertable = new AqlPrimitive(collectionName);
                return node;
            }
            
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
                if (node.Expression != null && node.Expression.Type == typeof(string) && node.Member.Name == "Length")
                {
                    var left = AqlExpressionConverter.ParseTerm(node.Expression, _context);
                    _aqlConvertable = new AqlFunction("CHAR_LENGTH", new [] {left});
                    return node;
                }
                else if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
                {
                    var left = AqlExpressionConverter.ParseTerm(node.Expression, _context);
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
                else if (node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(IQueryable<>))
                {
                    Expression<Func<IQueryable>> getQueryableExpr = Expression.Lambda<Func<IQueryable>>(node);
                    var getQueryable = getQueryableExpr.Compile();
                    var q = getQueryable();
                    
                    var v = _context.GetQueryiableDefinitionParameter(q, node.Member.Name);
                    _aqlConvertable = new AqlVariable(v);
                    
                    var e = q.Expression;

                    return node;
                }
                else if (node.Expression == null || node.Expression.NodeType == ExpressionType.Constant)
                {
                    Expression<Func<object>> le = Expression.Lambda<Func<object>>(Expression.Convert(node, typeof(object)));
                    var compiledExpression = le.Compile();
                    var value = compiledExpression();
                    _aqlConvertable = new AqlConstant(value, node.Member.Name);
                    return node;
                }
                else
                {
                    var left = AqlExpressionConverter.ParseTerm(node.Expression, _context);
                    
                    var memberName = node.Member.Name;
                    
                    _aqlConvertable = new AqlMemberAccess(left, memberName);
                    return node;
                }
                
            }

            
            throw new UnhandledExpressionException(node);
        }
        
        

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (AqlUnary.SupportedExpressionTypeOperators.TryGetValue(node.NodeType, out var operatorStr))
            {
                var body = AqlExpressionConverter.ParseTerm(node.Operand, _context);
                _aqlConvertable = new AqlUnary(body, operatorStr);
                return node;
            }
            
            throw new UnhandledExpressionException(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Add && node.Type == typeof(string))
            {
                var left = AqlExpressionConverter.ParseTerm(node.Left, _context);
                var right = AqlExpressionConverter.ParseTerm(node.Right, _context);
                _aqlConvertable = new AqlConcat(left, right, "strAdd");
                return node;
            }
            else if (AqlBinary.SupportedExpressionTypeOperators.TryGetValue(node.NodeType, out var operatorStr))
            {
                var left = AqlExpressionConverter.ParseTerm(node.Left, _context);
                var right = AqlExpressionConverter.ParseTerm(node.Right, _context);
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



    public class AqlDateAdd : AqlFunction
    {

        public AqlDateAdd(AqlConvertable date, AqlConvertable amount, AqlConvertable unit) : base("DATE_ADD", new []{ date, amount, unit })
        {
        }
        
    }

    public class AqlConcat : AqlConvertable
    {
        public AqlConvertable Left { get; }
        public AqlConvertable Right { get; }
        public string PreferredName { get; }

        public bool Equals(AqlConcat other)
        {
            return other != null
                   && this.Left == other.Left
                   && this.Right == other.Right
                   && this.PreferredName == other.PreferredName;
        }
        
        
        public AqlConcat(AqlConvertable left, AqlConvertable right, string preferredName) : base(false)
        {
            Left = left;
            Right = right;
            PreferredName = preferredName;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitConcat(this);
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

            
            if (TryGetStringFromStringConstant(Left, out var l) &&
                TryGetStringFromStringConstant(Right, out var r))
            {
                var result = l + r;
        
                var c = new AqlConstant(result, PreferredName);
        
                return c.Convert(parameters, bindVars);

            }

            var cLeft = Left.Convert(parameters, bindVars);
            var cRight = Right.Convert(parameters, bindVars);

            return $"CONCAT({cLeft}, {cRight})";
            
        }
    }

    public class AqlReturnObjectProjection : AqlConvertable
    {
        public Dictionary<string, AqlConvertable> MemberDict { get; } = new Dictionary<string, AqlConvertable>();

        public bool Equals(AqlReturnObjectProjection other)
        {
            return other != null
                   && this.MemberDict.SequenceEqual(other.MemberDict);
        }

        public AqlReturnObjectProjection() : base(false)
        {
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitReturnObjectProjection(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var sb = new StringBuilder();

            sb.AppendLine("{");

            var enumerator = MemberDict.GetEnumerator();


            var memberAssignments = MemberDict.Select(x =>
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
            MemberDict.Add(member, value);
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