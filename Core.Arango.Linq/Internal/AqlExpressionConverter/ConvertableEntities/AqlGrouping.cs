using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    }
    
    public class AqlGrouping : AqlConvertable, BuildStackConsumer, AqlParseQueryContextBuildStackElement
    {
        private readonly AqlGroupingKeyProjection _keyProjection;
        private string _outerParameter;
        private AqlSimpleSelect _selectBlock;

        public AqlGrouping(AqlGroupingKeyProjection keyProjection) : base(false)
        {
            _keyProjection = keyProjection;
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

            var groupVar = "g";


            var convertedKeySetters = new List<string>();
            foreach (var projKey in _keyProjection.GetMembers())
            {
                var p = BaseParamsWith(_keyProjection.Parameter, _outerParameter);
                
                var val = projKey.Value.Convert(p, bindVars, true);
                var setter = $"{projKey.Variable.Name} = {val}";
                convertedKeySetters.Add(setter);
            }
                
            
            var keysString = String.Join(", ", convertedKeySetters);

            

            sb.AppendLine($"COLLECT {keysString} INTO {groupVar}");
            
            
            // var postGroupParams = new Dictionary<string, string>(parameters);
            
            
            var selectString = _selectBlock.Body.Convert(
                BaseParamsWith(_selectBlock.Parameter.Name, groupVar),
                bindVars
            );
            sb.AppendLine($"RETURN {selectString}");
            
            
            // sb.AppendLine($"RETURN {groupVar}");
            

            return sb.ToString();
        }

        public void ConsumeFilter(AqlFilter filter)
        {
            throw new System.NotImplementedException();
        }

        public void SetSelect(AqlSimpleSelect selectBlock)
        {
            this._selectBlock = selectBlock;
        }
        
        public void ConsumeSelect(AqlSimpleSelect aqlSimpleSelect)
        {
            SetSelect(aqlSimpleSelect);
        }

        public void ConsumeGrouping(AqlGrouping aqlGrouping)
        {
            throw new System.NotImplementedException();
        }

        public void FeedToConsumer(BuildStackConsumer consumer)
        {
            consumer.ConsumeGrouping(this);
        }

        public void SetParameter(string collectionParameter)
        {
            this._outerParameter = collectionParameter;
        }
    }
}