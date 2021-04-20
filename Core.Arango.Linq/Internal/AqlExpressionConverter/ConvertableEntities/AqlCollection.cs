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
        private readonly AqlCollection _collection;
        private readonly bool _useFirstSelectAsReturn;
        private bool _useNextSelectAsReturn;
        
        private bool alreadyParsed = false;

        public AqlCollectionExpressionParser(AqlCollection collection, bool useFirstSelectAsReturn = false)
        {
            _collection = collection;
            _useFirstSelectAsReturn = useFirstSelectAsReturn;
        }


        public void Parse(Expression collectionExpression)
        {
            if (alreadyParsed)
                throw new Exception("Parser is one time use only!");
            
            this._useNextSelectAsReturn = _useFirstSelectAsReturn;
            var inner = ParseLayer(collectionExpression);
            _collection.Collection = inner;
            alreadyParsed = true;
        }

        private  AqlConvertable ParseLayer(Expression node)
        {
            if (node.NodeType == ExpressionType.Call)
            {
                return ParseMethod((MethodCallExpression) node);
            }

            return AqlExpressionConverter.ParseTerm(node);
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
                    case "SingleOrDefault":
                    {
                        _collection.OutputBehaviour = AqlQueryOutputBehaviour.SingleOrDefault;

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

                        var body = AqlExpressionConverter.ParseTerm(whereLambda.Body);
                        var parameter = whereLambda.Parameters[0];
                    
                        var whereBlock = new AqlFilter() {Body = body, Parameter = parameter};
                        _collection.AddFilterBlock(whereBlock);
                        
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

                                var proj = new AqlReturnObjectProjection();

                                for (var i = 0; i < expr.Members.Count; i++)
                                {
                                    var member = expr.Members[i].Name;
                                    var value = AqlExpressionConverter.ParseTerm(expr.Arguments[i]);
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
                                        var value = AqlExpressionConverter.ParseTerm(assignment.Expression);

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
                                body = AqlExpressionConverter.ParseTerm(selectLambda.Body);
                            }
                            #endregion

                            return body;
                        }
                        
                        if (_useNextSelectAsReturn)
                        {
                            _useNextSelectAsReturn = false;

                            var inner = node.Arguments[0];
                            var selectLambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));
                            var parameter = selectLambda.Parameters[0];

                            var body = GetSelectBody(selectLambda);

                            _collection.SetSelect(new AqlSimpleSelect(){ Body = body, Parameter = parameter });

                            return ParseLayer(inner);
                        }
                        else
                        {
                            var inner = node.Arguments[0];
                            var selectCollection = AqlExpressionConverter.ParseCollection(inner);
                            var lambda = (LambdaExpression) (UnquoteExpression(node.Arguments[1]));
                            var parameter = lambda.Parameters[0];
                            selectCollection.SetParameter(parameter);
                        
                            var body = GetSelectBody(lambda);
                        
                            selectCollection.SetSelect(new AqlSimpleSelect(){ Body = body, Parameter = parameter });

                            return selectCollection;
                        }
                    }
                    case "Take":
                    {
                        
                        Expression<Func<int>> limitValueExpression = Expression.Lambda<Func<int>>(node.Arguments[1]);
                        var compiledExpression = limitValueExpression.Compile();
                        var limitValue = compiledExpression();
                        var inner = node.Arguments[0];
                        
                        _collection.SetLimit(limitValue);
                        
                        return ParseLayer(inner);
                    }
                    case "OrderBy":
                    {
                        
                        var orderByLambda = (LambdaExpression) (((UnaryExpression) node.Arguments[1]).Operand);
                        var body = AqlExpressionConverter.ParseTerm(orderByLambda.Body);
                        var parameter = orderByLambda.Parameters[0];
                        
                        var aqlSort = new AqlSort() { Body = body, Parameter = parameter};
                        
                        var inner = node.Arguments[0];
                        
                        _collection.SetSort(aqlSort);
                        
                        return ParseLayer(inner);
                    }
                }
            }
            
            return AqlExpressionConverter.ParseTerm(node);
        }
    }
    
    public class AqlCollection : AqlConvertable
    {
        private readonly bool _withBrackets;
        private AqlSimpleSelect _selectBlock = null;
        private int? _limit;
        private AqlSort _sortBlock = null;
        private ParameterExpression _parameter;


        /// <summary>
        /// Requested output behaviour for the collection. For now it is only used for the toplevel query collection.
        /// </summary>
        public AqlQueryOutputBehaviour OutputBehaviour { get; set; } = AqlQueryOutputBehaviour.NormalList;


        public List<AqlFilter> filterBlocks { get; set; } = new List<AqlFilter>();
        public AqlConvertable Collection { get; set; }


        public AqlCollection(bool withBrackets = true) : base(true)
        {
            _withBrackets = withBrackets;
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            var projectionVarLabel = _parameter.Name;
            
            
            Dictionary<string, string> BaseParamsWith(string k, string v)
            {
                var d = new Dictionary<string, string>(parameters);
                d.TryAdd(k, v);
                return d;
            }


            var addBrackets = this.filterBlocks.Count > 1;

            
            var filters = filterBlocks
                .Select(x => 
                    x.Body.Convert(
                        BaseParamsWith(x.Parameter.Name, projectionVarLabel),
                        bindVars
                    ));
            if (addBrackets)
                filters = filters.Select(x => $"({x})");
            
            
            var filterString = String.Join(" && ", filters);

            var sb = new StringBuilder();
            
            if (_withBrackets)
                sb.AppendLine("(");

            var collectionAql = Collection.Convert(
                BaseParamsWith(projectionVarLabel, projectionVarLabel),
                bindVars);
            
            sb.AppendLine($"FOR {projectionVarLabel} IN {collectionAql}");
            
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
                    BaseParamsWith(_sortBlock.Parameter.Name, projectionVarLabel),
                    bindVars
                );
                sb.AppendLine($"SORT {sortString}");
            }

            if (_selectBlock != null)
            {
                var selectString = _selectBlock.Body.Convert(
                    BaseParamsWith(_selectBlock.Parameter.Name, projectionVarLabel),
                    bindVars
                );
                sb.AppendLine($"RETURN {selectString}");
            }
            else
            {
                sb.AppendLine($"RETURN {projectionVarLabel}");
            }

            if (_withBrackets)
                sb.AppendLine(")");

            return sb.ToString();
        }
        
        public void AddFilterBlock(AqlFilter filterBlock)
        {
            this.filterBlocks.Add(filterBlock);
        }

        public void SetSelect(AqlSimpleSelect selectBlock)
        {
            this._selectBlock = selectBlock;
        }

        public void SetLimit(int i)
        {
            this._limit = i;
        }
        
        public void SetParameter(ParameterExpression p)
        {
            this._parameter = p;
        }

        public void SetSort(AqlSort aqlSort)
        {
            this._sortBlock = aqlSort;
        }
        
    }

    public enum AqlQueryOutputBehaviour
    {
        NormalList,
        SingleOrDefault,
    }
}