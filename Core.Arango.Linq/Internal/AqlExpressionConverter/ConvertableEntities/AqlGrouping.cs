using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;

namespace Core.Arango.Linq.Internal
{

    public class AqlGroupingKeyMember
    {
        public string Member { get; set; }
        public AqlConvertable Value { get; set; }
        public AqlQueryVariable Variable { get; set; }
    }
    
    public class AqlGroupingKeyProjection
    {
        private List<AqlGroupingKeyMember> _members = new List<AqlGroupingKeyMember>();
        
        public string Parameter { get; }

        public AqlGroupingKeyProjection(string parameter)
        {
            Parameter = parameter;
        }

        public void AddMember(AqlGroupingKeyMember member)
        {
            _members.Add(member);
        }

        public IEnumerable<AqlGroupingKeyMember> GetMembers()
        {
            return _members;
        }

        public void AddMember(string member, AqlConvertable value, AqlQueryVariable memberVar)
        {
            AddMember(new AqlGroupingKeyMember() { Member =  member, Value = value, Variable = memberVar});
        }
    }

    /// <summary>
    /// Several Jobs:
    /// - Replaces group keys
    /// - Checks for direct collection access
    /// - Extracts collection aggregates
    /// </summary>
    internal class AqlGroupVisitor : AqlVisitor
    {
        public AqlGrouping Group { get; }
        public ParameterExpression GroupParameter { get; }
        public AqlParseQueryContext Context { get; } = new AqlParseQueryContext();
        

        public bool DirectCollectionAccess { get; private set; } = false;

        public AqlGroupVisitor(AqlGrouping @group, ParameterExpression groupParameter)
        {
            Group = group;
            GroupParameter = groupParameter;
        }

        public override AqlConvertable VisitParameter(AqlParameter node)
        {

            if (node.Expr == GroupParameter)
            {
                this.DirectCollectionAccess = true;
                return new AqlParameter(GroupParameter);
            }
            
            return base.VisitParameter(node);
        }

        public override AqlConvertable VisitFunction(AqlFunction node)
        {
            if (node.FunctionName == "LENGTH" && node.Arguments.Length > 0)
            {
                var groupParam = node.Arguments[0] as AqlParameter;
                if (groupParam.Expr == GroupParameter)
                {
                    
                    var v = Context.MakeNewVariable("aggr");
                    var varTerm = new AqlVariable(v);

                    var aggrTerm = new AqlFunction("LENGTH", new []{ new AqlPrimitive("1") });
                    
                    Group.AddAggregate(aggrTerm, varTerm);
                    return varTerm;
                }
            }
             
            
            
            return base.VisitFunction(node);
        }


        public override AqlConvertable VisitMemberAccess(AqlMemberAccess node)
        {
            var key = node.Left as AqlMemberAccess;
            var groupParam = key?.Left as AqlParameter;

            if (key != null && groupParam != null && key.Member == "_key" && groupParam.Expr == GroupParameter)
            {
                var v = Group.KeyProjection.GetMembers()
                    .Where(x => x.Member == node.Member)
                    .Select(x => x.Variable).Single();
                
                return new AqlVariable(v);
            }
            
            
            return base.VisitMemberAccess(node);
        }
    } 
    
    public class AqlGrouping : AqlConvertable, BuildStackConsumer, AqlParseQueryContextBuildStackElement
    {
        public AqlGroupingKeyProjection KeyProjection { get; }
        public string OuterParameter { get; private set; }
        public AqlSimpleSelect SelectBlock { get; private set; }
        
        public Dictionary<AqlVariable, AqlConvertable> Aggregates = new Dictionary<AqlVariable, AqlConvertable>();
        
        public AqlQueryVariable GroupVariable { get; }
        

        public AqlGrouping(AqlGroupingKeyProjection keyProjection, AqlQueryVariable groupVar) : base(false)
        {
            KeyProjection = keyProjection;
            GroupVariable = groupVar;
        }

        

        public bool Equals(AqlGrouping other)
        {
            return other != null
                   && this.KeyProjection == other.KeyProjection
                   && this.Aggregates.SequenceEqual(other.Aggregates)
                   && this.SelectBlock == other.SelectBlock;
        }

        public override AqlConvertable Accept(AqlVisitor visitor)
        {
            return visitor.VisitGroup(this);
        }

        public override string Convert(Dictionary<string, string> parameters, AqlBindVarsPool bindVars)
        {
            Dictionary<string, string> BaseParamsWith(string k, string v)
            {
                var d = new Dictionary<string, string>(parameters);
                d.TryAdd(k, v);
                return d;
            }
            
            
            var sb = new StringBuilder();

            var groupVar = GroupVariable.Name;

            var loadWholeCollection = false;
            
            AqlConvertable CorrectGroupParameter(AqlConvertable aql, ParameterExpression p)
            {
                var visitor = new AqlGroupVisitor(this, p);
                
                var ret = visitor.Visit(aql);
                
                if (visitor.DirectCollectionAccess) 
                    loadWholeCollection = true;
                
                return ret;
            }


            var convertedKeySetters = new List<string>();
            foreach (var projKey in KeyProjection.GetMembers())
            {
                var p = BaseParamsWith(KeyProjection.Parameter, OuterParameter);
                var val = projKey.Value.Convert(p, bindVars, true);
                var setter = $"{projKey.Variable.Name} = {val}";
                convertedKeySetters.Add(setter);
            }
                
            
            var keysString = String.Join(", ", convertedKeySetters);

            // var postGroupParams = new Dictionary<string, string>(parameters);


            var temp = CorrectGroupParameter(SelectBlock.Body, SelectBlock.Parameter);
            var selectString = temp.Convert(BaseParamsWith(SelectBlock.Parameter.Name, groupVar),
                bindVars
            );
            
            
            sb.AppendLine($"COLLECT {keysString}");
            if (loadWholeCollection)
                sb.AppendLine($"INTO {groupVar}");

            if (this.Aggregates.Any())
            {
                var convertedAggregates = new List<string>();
                foreach (var aggr in Aggregates)
                {
                    var p = BaseParamsWith(KeyProjection.Parameter, OuterParameter);
                    var var = aggr.Key.Convert(p, bindVars, true);
                    var val = aggr.Value.Convert(p, bindVars, true);
                    var setter = $"{var} = {val}";
                    convertedAggregates.Add(setter);
                }
                var aggrStr = String.Join(", ", convertedAggregates);
                sb.AppendLine($"AGGREGATE {aggrStr}");
            }


            sb.AppendLine($"RETURN {selectString}");
            
            
            // sb.AppendLine($"RETURN {groupVar}");
            

            return sb.ToString();
        }
        
        public void AddAggregate(AqlConvertable term, AqlVariable var)
        {
            this.Aggregates.Add(var, term);
        }

        public void ConsumeFilter(AqlFilter filter)
        {
            throw new System.NotImplementedException();
        }

        public void SetSelect(AqlSimpleSelect selectBlock)
        {
            this.SelectBlock = selectBlock;
        }
        
        public void ConsumeSelect(AqlSimpleSelect aqlSimpleSelect)
        {
            SetSelect(aqlSimpleSelect);
        }

        public void ConsumeGrouping(AqlGrouping aqlGrouping)
        {
            throw new System.NotImplementedException();
        }

        public void ConsumeSort(AqlSort aqlSort)
        {
            throw new NotImplementedException();
        }

        public void ConsumeLimit(AqlLimit aqlLimit)
        {
            throw new NotImplementedException();
        }

        public void ConsumeOutputBehaviour(AqlOutputBehaviour aqlOutputBehaviour)
        {
            throw new NotImplementedException();
        }

        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeGrouping(this);
        }

        public void SetParameter(string collectionParameter)
        {
            this.OuterParameter = collectionParameter;
        }
    }
}