using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Newtonsoft.Json.Serialization;

namespace Core.Arango.Linq.Internal
{
    
    public class AqlCollectionExpressionParser
    {
        private readonly AqlParseQueryContext _context;
        private readonly bool _useFirstSelectAsReturn;
        private bool _useNextSelectAsReturn;
        
        private bool alreadyParsed = false;

        public AqlCollectionExpressionParser(AqlParseQueryContext context, bool useFirstSelectAsReturn = false)
        {
            _context = context;
            _useFirstSelectAsReturn = useFirstSelectAsReturn;
        }


        public AqlCollection Parse(Expression collectionExpression)
        {
            if (alreadyParsed)
                throw new Exception("Parser is one time use only!");
            
            this._useNextSelectAsReturn = _useFirstSelectAsReturn;
            var inner = ParseLayer(collectionExpression);
            // _collection.Collection = inner;
            // _context.ConsumeBuildStack(_collection);
            
            alreadyParsed = true;

            if (!_context.BuildStackElements.Any()  && inner is AqlCollection)
            {
                return (AqlCollection) inner;
            }
            else
            {
                var c = new AqlCollection(false);
                c.Collection = inner;
                _context.ConsumeBuildStack(c);

                return c;
            }
        }

        private  AqlConvertable ParseLayer(Expression node)
        {
            if (node.NodeType == ExpressionType.Call)
            {
                return ParseMethod((MethodCallExpression) node);
            }

            return AqlExpressionConverter.ParseTerm(node, _context);
        }

        private  AqlConvertable ParseMethod(MethodCallExpression node)
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
            
            if (node.Method.DeclaringType ==  typeof(System.Linq.Enumerable) || node.Method.DeclaringType == typeof(System.Linq.Queryable))
            {
                
                
                switch (node.Method.Name)
                {
                    case "Distinct":
                    {
                        var d = new AqlDistinctElement();
                        
                        _context.AddElementToBuildStack(d);
                        
                        var inner = node.Arguments[0];
                        return ParseLayer(node.Arguments[0]);
                    }
                    case "SingleOrDefault":
                    {
                        var b = new AqlOutputBehaviour(AqlQueryOutputBehaviour.SingleOrDefault);
                        
                        _context.AddElementToBuildStack(b);

                        if (node.Arguments.Count > 1)
                        {
                            goto case "Where";
                        }

                        var inner = node.Arguments[0];
                        return ParseLayer(node.Arguments[0]);
                    }
                    case "Where":
                    {
                        var whereLambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));

                        var body = AqlExpressionConverter.ParseTerm(whereLambda.Body, _context);
                        var parameter = whereLambda.Parameters[0];
                    
                        var whereBlock = new AqlFilter() {Body = body, Parameter = parameter};
                        _context.AddElementToBuildStack(whereBlock);
                        // _collection.AddFilterBlock(whereBlock);
                        
                        var inner = node.Arguments[0];
                        return ParseLayer(inner);
                    }
                    case "GroupBy":
                    {
                        
                        AqlGroupingKeyProjection GetGroupingKeyProjection(LambdaExpression keyLambda)
                        {
                            AqlGroupingKeyProjection body;
                            #region get body
                            if (keyLambda.Body.NodeType == ExpressionType.New)
                            {
                                var expr = (NewExpression) keyLambda.Body;
                                var param = keyLambda.Parameters[0];

                                var proj = new AqlGroupingKeyProjection(param.Name);

                                for (var i = 0; i < expr.Members.Count; i++)
                                {
                                    var member = expr.Members[i].Name;
                                    var value = AqlExpressionConverter.ParseTerm(expr.Arguments[i], _context);
                                    var memberVar = _context.MakeNewVariable(member);
                                    
                                    proj.AddMember(member, value, memberVar);
                                }

                                body = proj;
                            }
                            else if (keyLambda.Body.NodeType == ExpressionType.MemberInit)
                            {
                                var init = (MemberInitExpression) keyLambda.Body;
                                
                                var param = keyLambda.Parameters[0];
                                var proj = new AqlGroupingKeyProjection(param.Name);

                                foreach (var binding in init.Bindings)
                                {
                                    if (binding.BindingType == MemberBindingType.Assignment)
                                    {
                                        var assignment = (MemberAssignment) binding;
                                        var member = assignment.Member.Name;
                                        var value = AqlExpressionConverter.ParseTerm(assignment.Expression, _context);
                                        var memberVar = _context.MakeNewVariable(member);

                                        proj.AddMember(member, value, memberVar);

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
                                body = null;
                            }
                            #endregion

                            return body;
                        }
                        
                        var test = "hallo";

                        var keyProjection = (LambdaExpression) UnquoteExpression(node.Arguments[1]);

                        var groupKeys = GetGroupingKeyProjection( keyProjection);

                        var test2 = "test2";

                        var gv = _context.MakeNewVariable("g");
                        var grouping = new AqlGrouping(groupKeys, gv);
                        _context.ConsumeBuildStack(grouping);
                        _context.AddElementToBuildStack(grouping);
                        
                        var inner = node.Arguments[0];
                        return ParseLayer(inner);
                    }
                    case "Select":
                    {
                        AqlConvertable GetSelectBody(LambdaExpression selectLambda)
                        {
                            AqlConvertable body;
                            #region get body
                            if (selectLambda.Body.NodeType == ExpressionType.New)
                            {
                                var expr = (NewExpression) selectLambda.Body;

                                var proj = new AqlObjectProjection();

                                for (var i = 0; i < expr.Members.Count; i++)
                                {
                                    var member = expr.Members[i].Name;
                                    var value = AqlExpressionConverter.ParseTerm(expr.Arguments[i], _context);
                                    proj.AddMember(member, value);
                                }

                                body = proj;
                            }
                            else if (selectLambda.Body.NodeType == ExpressionType.MemberInit)
                            {
                                var init = (MemberInitExpression) selectLambda.Body;

                                var proj = new AqlObjectProjection();

                                foreach (var binding in init.Bindings)
                                {
                                    if (binding.BindingType == MemberBindingType.Assignment)
                                    {
                                        var assignment = (MemberAssignment) binding;
                                        var member = assignment.Member.Name;
                                        var value = AqlExpressionConverter.ParseTerm(assignment.Expression, _context);

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
                                body = AqlExpressionConverter.ParseTerm(selectLambda.Body, _context);
                            }
                            #endregion

                            return body;
                        }

                        // var alreadySelectOnStack = _context.BuildStackElements.Any(x => x is AqlSimpleSelect);
                        
                        
                        {
                            
                            
                            
                            var inner = node.Arguments[0];
                            var selectLambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));
                            var parameter = selectLambda.Parameters[0];

                            var body = GetSelectBody(selectLambda);

                            var select = new AqlSimpleSelect() {Body = body, Parameter = parameter};


                            var subSelectRequired =
                                _context.BuildStackElements.Any(x => x is AqlFilter || x is AqlSimpleSelect);

                            if (subSelectRequired)
                            {

                                var coll = new AqlCollection();
                                var paramVar = _context.MakeNewVariable("q");
                                var param = Expression.Parameter(typeof(object), paramVar.Name);
                                coll.SetParameter(param);
                                
                                var tempStack = _context.BuildStackElements.ToList();
                                _context.BuildStackElements.Clear();

                                _context.AddElementToBuildStack(select);
                                coll.Collection = ParseLayer(inner);
                                _context.ConsumeBuildStack(coll);

                                tempStack.Reverse();
                                foreach (var e in tempStack)
                                {
                                    _context.AddElementToBuildStack(e);
                                }
                                

                                return coll;
                            }
                            else
                            {
                                _context.AddElementToBuildStack(select);

                                return ParseLayer(inner);
                            }
                        }

                        #region old select handling

                        if (_useNextSelectAsReturn)
                        {
                            _useNextSelectAsReturn = false;

                            var inner = node.Arguments[0];
                            var selectLambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));
                            var parameter = selectLambda.Parameters[0];

                            var body = GetSelectBody(selectLambda);

                            var select = new AqlSimpleSelect() {Body = body, Parameter = parameter};
                            
                            _context.AddElementToBuildStack(select);

                            return ParseLayer(inner);
                        }
                        else
                        {
                            var inner = node.Arguments[0];
                            var selectCollection = AqlExpressionConverter.ParseCollection(inner, _context);
                            var lambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));
                            var parameter = lambda.Parameters[0];
                            selectCollection.SetParameter(parameter);
                        
                            var body = GetSelectBody(lambda);
                        
                            selectCollection.SetSelect(new AqlSimpleSelect(){ Body = body, Parameter = parameter });

                            return selectCollection;
                        }

                        #endregion
                        
                    }
                    case "Take":
                    {
                        
                        Expression<Func<int>> limitValueExpression = Expression.Lambda<Func<int>>(node.Arguments[1]);
                        var compiledExpression = limitValueExpression.Compile();
                        var limitValue = compiledExpression();
                        var inner = node.Arguments[0];
                        
                        var limit = new AqlLimit(limitValue);
                        
                        _context.AddElementToBuildStack(limit);
                        
                        // throw new NotImplementedException();
                        //_collection.SetLimit(limitValue);
                        
                        return ParseLayer(inner);
                    }
                    case "OrderBy":
                    {
                        
                        var orderByLambda = (LambdaExpression) (((UnaryExpression) node.Arguments[1]).Operand);
                        var body = AqlExpressionConverter.ParseTerm(orderByLambda.Body, _context);
                        var parameter = orderByLambda.Parameters[0];
                        
                        var aqlSort = new AqlSort() { Body = body, Parameter = parameter};
                        
                        var inner = node.Arguments[0];
                        
                        _context.AddElementToBuildStack(aqlSort);
                        
                        // throw new NotImplementedException();
                        // _collection.SetSort(aqlSort);
                        
                        return ParseLayer(inner);
                    }
                }
            }
            
            return AqlExpressionConverter.ParseTerm(node, _context);
        }
    }
    
    public class AqlCollection : AqlConvertable, BuildStackConsumer
    {
        public bool WithBrackets { get; set; }
        public AqlSimpleSelect SelectBlock { get; private set; } = null;
        public int? Limit { get; private set; }
        public AqlSort SortBlock { get; private set; } = null;
        public ParameterExpression Parameter { get; private set; }
        public AqlGrouping Grouping { get; private set; } = null;


        /// <summary>
        /// Requested output behaviour for the collection. For now it is only used for the toplevel query collection.
        /// </summary>
        public AqlQueryOutputBehaviour OutputBehaviour { get; set; } = AqlQueryOutputBehaviour.NormalList;


        public List<AqlFilter> FilterBlocks { get; set; } = new List<AqlFilter>();
        public AqlConvertable Collection { get; set; }
        
        public bool DistinctResult { get; set; }

        public AqlCollection(bool withBrackets = true) : base(true)
        {
            WithBrackets = withBrackets;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitCollection(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var projectionVarLabel = Parameter.Name;
            
            
            Dictionary<string, string> BaseParamsWith(string k, string v)
            {
                var d = new Dictionary<string, string>(parameters);
                d.TryAdd(k, v);
                return d;
            }


            var addBrackets = this.FilterBlocks.Count > 1;

            
            var filters = FilterBlocks
                .Select(x => 
                    x.Body.Convert(
                        BaseParamsWith(x.Parameter.Name, projectionVarLabel),
                        bindVars
                    ));
            if (addBrackets)
                filters = filters.Select(x => $"({x})");
            
            
            var filterString = String.Join(" && ", filters);

            var sb = new StringBuilder();
            
            if (WithBrackets)
                sb.AppendLine("(");

            var collectionAql = Collection.Convert(
                parameters,
                bindVars);
            
            sb.AppendLine($"FOR {projectionVarLabel} IN {collectionAql}");
            
            if (filterString.Length > 0)
            {
                sb.AppendLine($"FILTER {filterString}");
            }

            if (Limit.HasValue)
            {
                sb.AppendLine($"LIMIT {Limit.Value}");
            }

            if (SortBlock != null)
            {
                var sortString = SortBlock.Body.Convert(
                    BaseParamsWith(SortBlock.Parameter.Name, projectionVarLabel),
                    bindVars
                );
                sb.AppendLine($"SORT {sortString}");
            }

            if (Grouping != null)
            {
                Grouping.SetParameter(projectionVarLabel);
                var groupingStr = Grouping.Convert(parameters, bindVars);
                sb.AppendLine($"{groupingStr}");
            }
            else
            {
                string returnValue;
                if (SelectBlock != null)
                {
                    var selectString = SelectBlock.Body.Convert(
                        BaseParamsWith(SelectBlock.Parameter.Name, projectionVarLabel),
                        bindVars
                    );
                    returnValue = selectString;
                }
                else
                {
                    returnValue = projectionVarLabel;
                    
                }

                if (DistinctResult)
                {
                    returnValue = $"DISTINCT ({returnValue})";
                }
                
                sb.AppendLine($"RETURN {returnValue}");
            }

            

            if (WithBrackets)
                sb.AppendLine(")");

            return sb.ToString();
        }
        
        public void AddFilterBlock(AqlFilter filterBlock)
        {
            this.FilterBlocks.Add(filterBlock);
        }

        public void SetSelect(AqlSimpleSelect selectBlock)
        {
            this.SelectBlock = selectBlock;
        }

        public void SetLimit(int i)
        {
            this.Limit = i;
        }
        
        public void SetParameter(ParameterExpression p)
        {
            this.Parameter = p;
        }

        public void SetSort(AqlSort aqlSort)
        {
            this.SortBlock = aqlSort;
        }

        public void ConsumeFilter(AqlFilter filter)
        {
            AddFilterBlock(filter);
        }

        public void ConsumeSelect(AqlSimpleSelect aqlSimpleSelect)
        {
            SetSelect(aqlSimpleSelect);
        }

        public void ConsumeGrouping(AqlGrouping aqlGrouping)
        {
            SetGrouping(aqlGrouping);
        }

        public void ConsumeSort(AqlSort aqlSort)
        {
            this.SetSort(aqlSort);
        }

        public void ConsumeLimit(AqlLimit aqlLimit)
        {
            this.Limit = aqlLimit.Limit;
        }

        public void ConsumeOutputBehaviour(AqlOutputBehaviour aqlOutputBehaviour)
        {
            this.OutputBehaviour = aqlOutputBehaviour.Behaviour;
        }

        public void ConsumeDistinct(AqlDistinctElement aqlDistinctElement)
        {
            this.DistinctResult = true;
        }

        

        private void SetGrouping(AqlGrouping aqlGrouping)
        {
            this.Grouping = aqlGrouping;
        }
    }

    public enum AqlQueryOutputBehaviour
    {
        NormalList,
        SingleOrDefault,
    }
}